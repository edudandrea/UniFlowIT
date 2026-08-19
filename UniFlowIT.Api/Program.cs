using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniFlowIT.Api.Data;
using UniFlowIT.Api.Models;
using UniFlowIT.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var ticketTypingStates = new ConcurrentDictionary<string, TypingState>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>() ?? [];
        if (builder.Environment.IsDevelopment() || allowedOrigins.Length == 0)
        {
            policy.SetIsOriginAllowed(origin =>
                Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                && (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
                    || uri.Host.Equals("::1", StringComparison.OrdinalIgnoreCase)))
                .AllowAnyHeader()
                .AllowAnyMethod();
            return;
        }

        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024;
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSingleton<AuthTokenService>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.UseCors();
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    var csp = context.Request.Path.StartsWithSegments("/api/equipamentos/publico")
        ? "default-src 'none'; style-src 'unsafe-inline'; frame-ancestors 'none'"
        : "default-src 'none'; frame-ancestors 'none'";
    context.Response.Headers.TryAdd("Content-Security-Policy", csp);
    await next();
});

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/api")
        || context.Request.Path.StartsWithSegments("/api/auth/bootstrap-status")
        || context.Request.Path.StartsWithSegments("/api/auth/login")
        || context.Request.Path.StartsWithSegments("/api/auth/criar-administrador-saas")
        || context.Request.Path.StartsWithSegments("/api/agent/installer")
        || context.Request.Path.StartsWithSegments("/api/agent/download")
        || context.Request.Path.StartsWithSegments("/api/equipamentos/publico"))
    {
        await next();
        return;
    }

    var token = context.Request.Headers.Authorization.ToString();
    token = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? token["Bearer ".Length..].Trim() : null;
    var tokenService = context.RequestServices.GetRequiredService<AuthTokenService>();
    if (!tokenService.TryValidate(token, out var session))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    var db = context.RequestServices.GetRequiredService<AppDbContext>();
    var usuarioAtivo = await db.Users
        .Include(user => user.Empresa)
        .AnyAsync(user => user.Id == session.UserId
            && user.Email == session.Email
            && user.Role == session.Role
            && user.Ativo
            && (user.Empresa == null || user.Empresa.Ativo && !user.Empresa.AcessoBloqueado));

    if (!usuarioAtivo)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    context.Items["AuthSession"] = session;
    await next();
});

app.MapGet("/", () => Results.Ok(new { name = "UniFlowIT.Api", phase = "Fase 1 - Central de tickets" }));

app.MapGet("/api/auth/bootstrap-status", async (AppDbContext db) =>
{
    var existeAdministradorSaas = await db.Users.AnyAsync(user => user.Role.ToLower() == "administradorsaas");
    return Results.Ok(new { existeAdministradorSaas });
});

app.MapPost("/api/auth/criar-administrador-saas", async (AppDbContext db, AuthTokenService tokenService, CriarAdministradorSaasRequest request) =>
{
    var existeAdministradorSaas = await db.Users.AnyAsync(user => user.Role.ToLower() == "administradorsaas");
    if (existeAdministradorSaas)
    {
        return Results.Conflict(new { message = "O Administrador SaaS inicial ja foi criado." });
    }

    if (!PasswordService.IsStrong(request.Senha))
    {
        return Results.BadRequest(new { message = "A senha deve ter no minimo 8 caracteres, letra maiuscula, numero e caractere especial." });
    }

    var email = request.Email.Trim().ToLowerInvariant();
    var usuario = new Users
    {
        Nome = request.Nome.Trim(),
        Login = email,
        Email = email,
        SenhaHash = PasswordService.Hash(request.Senha),
        Role = "AdministradorSaas",
        EmpresaId = null,
        Ativo = true
    };

    db.Users.Add(usuario);
    await db.SaveChangesAsync();

    return Results.Created($"/api/usuarios/{usuario.Id}", CriarAuthResponse(usuario, tokenService));
});

app.MapPost("/api/auth/login", async (AppDbContext db, AuthTokenService tokenService, LoginRequest request) =>
{
    var email = request.Email.Trim().ToLowerInvariant();
    var usuario = await db.Users
        .Include(user => user.Empresa)
        .FirstOrDefaultAsync(user => user.Email.ToLower() == email);

    if (usuario is null || !usuario.Ativo || !PasswordService.Verify(request.Senha, usuario.SenhaHash))
    {
        return Results.Unauthorized();
    }

    if (usuario.Empresa is { Ativo: false } || usuario.Empresa?.AcessoBloqueado == true)
    {
        return Results.Forbid();
    }

    return Results.Ok(CriarAuthResponse(usuario, tokenService));
});

app.MapGet("/api/empresas", async (AppDbContext db, HttpContext http, int? contratanteId, bool somenteContratantes = false) =>
{
    var auth = Auth(http);
    var query = db.Empresas.AsQueryable();

    if (IsSaas(auth) && somenteContratantes)
    {
        query = query.Where(empresa => empresa.EmpresaContratanteId == null);
    }

    if (IsSaas(auth) && contratanteId.HasValue)
    {
        query = query.Where(empresa => empresa.Id == contratanteId || empresa.EmpresaContratanteId == contratanteId);
    }
    else if (!IsSaas(auth))
    {
        if (!auth.EmpresaId.HasValue)
        {
            return Results.Forbid();
        }

        query = query.Where(empresa => empresa.Id == auth.EmpresaId || empresa.EmpresaContratanteId == auth.EmpresaId);
    }

    var empresas = await query.OrderBy(empresa => empresa.Nome).ToListAsync();

    return Results.Ok(empresas);
});

app.MapPost("/api/empresas", async (AppDbContext db, HttpContext http, CriarEmpresaRequest request) =>
{
    var auth = Auth(http);
    if (!IsSaas(auth) && !IsCompanyAdmin(auth))
    {
        return Results.Forbid();
    }

    var erroValidacao = ValidarEmpresaRequest(request);
    if (erroValidacao is not null)
    {
        return Results.BadRequest(new { message = erroValidacao });
    }

    if (!IsSaas(auth))
    {
        request.EmpresaContratanteId = auth.EmpresaId;
        request.Ativo = true;
        request.AcessoBloqueado = false;
        request.MotivoBloqueio = null;
        request.BloqueadoEm = null;
    }

    var slug = NormalizarTenantSlug(request.TenantSlug);
    var slugExiste = await db.Empresas.AnyAsync(empresa => empresa.TenantSlug == slug);
    if (slugExiste)
    {
        return Results.Conflict(new { message = "Ja existe uma empresa com este identificador de tenant." });
    }

    var empresa = new Empresa
    {
        Nome = string.IsNullOrWhiteSpace(request.NomeFantasia) ? request.Nome.Trim() : request.NomeFantasia.Trim(),
        RazaoSocial = string.IsNullOrWhiteSpace(request.RazaoSocial) ? request.Nome.Trim() : request.RazaoSocial.Trim(),
        NomeFantasia = string.IsNullOrWhiteSpace(request.NomeFantasia) ? request.Nome.Trim() : request.NomeFantasia.Trim(),
        TenantSlug = slug,
        Cnpj = request.Cnpj.Trim(),
        Email = request.Email.Trim().ToLowerInvariant(),
        Telefone = request.Telefone.Trim(),
        Endereco = request.Endereco.Trim(),
        Numero = request.Numero.Trim(),
        Complemento = request.Complemento,
        Bairro = request.Bairro,
        Cidade = request.Cidade,
        Estado = request.Estado,
        Cep = request.Cep,
        InscricaoMunicipal = request.InscricaoMunicipal,
        InscricaoEstadual = request.InscricaoEstadual,
        LogoUrl = request.LogoUrl,
        EmpresaContratanteId = request.EmpresaContratanteId,
        TipoUnidade = NormalizarTipoUnidade(request.TipoUnidade, request.EmpresaContratanteId),
        Ativo = request.Ativo,
        AcessoBloqueado = request.AcessoBloqueado,
        MotivoBloqueio = request.MotivoBloqueio,
        BloqueadoEm = request.BloqueadoEm,
        DataCadastro = DateTime.UtcNow.Date
    };

    db.Empresas.Add(empresa);
    await db.SaveChangesAsync();

    return Results.Created($"/api/empresas/{empresa.Id}", empresa);
});

app.MapPut("/api/empresas/{id:int}", async (AppDbContext db, HttpContext http, int id, CriarEmpresaRequest request) =>
{
    var auth = Auth(http);
    if (!IsSaas(auth) && !IsCompanyAdmin(auth))
    {
        return Results.Forbid();
    }

    var erroValidacao = ValidarEmpresaRequest(request);
    if (erroValidacao is not null)
    {
        return Results.BadRequest(new { message = erroValidacao });
    }

    var empresa = await db.Empresas.FindAsync(id);
    if (empresa is null)
    {
        return Results.NotFound();
    }

    if (!IsSaas(auth))
    {
        if (!auth.EmpresaId.HasValue || empresa.Id != auth.EmpresaId && empresa.EmpresaContratanteId != auth.EmpresaId)
        {
            return Results.Forbid();
        }

        request.EmpresaContratanteId = empresa.Id == auth.EmpresaId ? null : auth.EmpresaId;
        request.Ativo = empresa.Ativo;
        request.AcessoBloqueado = empresa.AcessoBloqueado;
        request.MotivoBloqueio = empresa.MotivoBloqueio;
        request.BloqueadoEm = empresa.BloqueadoEm;
    }

    var slug = NormalizarTenantSlug(request.TenantSlug);
    var slugExiste = await db.Empresas.AnyAsync(item => item.Id != id && item.TenantSlug == slug);
    if (slugExiste)
    {
        return Results.Conflict(new { message = "Ja existe uma empresa com este identificador de tenant." });
    }

    empresa.Nome = string.IsNullOrWhiteSpace(request.NomeFantasia) ? request.Nome.Trim() : request.NomeFantasia.Trim();
    empresa.RazaoSocial = request.RazaoSocial.Trim();
    empresa.NomeFantasia = request.NomeFantasia.Trim();
    empresa.TenantSlug = slug;
    empresa.Cnpj = request.Cnpj.Trim();
    empresa.Telefone = request.Telefone.Trim();
    empresa.Email = request.Email.Trim().ToLowerInvariant();
    empresa.Endereco = request.Endereco.Trim();
    empresa.Numero = request.Numero.Trim();
    empresa.Complemento = request.Complemento;
    empresa.Bairro = request.Bairro;
    empresa.Cidade = request.Cidade;
    empresa.Estado = request.Estado;
    empresa.Cep = request.Cep;
    empresa.InscricaoMunicipal = request.InscricaoMunicipal;
    empresa.InscricaoEstadual = request.InscricaoEstadual;
    empresa.LogoUrl = request.LogoUrl;
    empresa.EmpresaContratanteId = request.EmpresaContratanteId;
    empresa.TipoUnidade = NormalizarTipoUnidade(request.TipoUnidade, request.EmpresaContratanteId);
    empresa.Ativo = request.Ativo;
    empresa.AcessoBloqueado = request.AcessoBloqueado;
    empresa.MotivoBloqueio = request.MotivoBloqueio;
    empresa.BloqueadoEm = request.BloqueadoEm;

    await db.SaveChangesAsync();
    return Results.Ok(empresa);
});

app.MapGet("/api/usuarios", async (AppDbContext db, HttpContext http, int? empresaId) =>
{
    var auth = Auth(http);
    if (!IsSaas(auth) && !IsCompanyAdmin(auth))
    {
        return Results.Forbid();
    }

    var query = db.Users
        .Include(user => user.Empresa)
        .AsQueryable();

    if (IsSaas(auth) && empresaId.HasValue)
    {
        query = query.Where(user => user.EmpresaId == empresaId);
    }
    else if (!IsSaas(auth))
    {
        var empresasPermitidas = await EmpresasPermitidas(db, auth);
        query = query.Where(user => user.EmpresaId.HasValue && empresasPermitidas.Contains(user.EmpresaId.Value));
    }

    var usuarios = await query
        .OrderBy(user => user.Empresa!.Nome)
        .ThenBy(user => user.Nome)
        .Select(user => new UsuarioResponse
        {
            Id = user.Id,
            EmpresaId = user.EmpresaId,
            EmpresaNome = user.Empresa != null ? user.Empresa.Nome : "SaaS",
            Nome = user.Nome,
            Email = user.Email,
            Role = user.Role,
            Ativo = user.Ativo
        })
        .ToListAsync();

    return Results.Ok(usuarios);
});

app.MapPost("/api/usuarios", async (AppDbContext db, HttpContext http, CriarUsuarioRequest request) =>
{
    var auth = Auth(http);
    if (!IsSaas(auth) && !IsCompanyAdmin(auth))
    {
        return Results.Forbid();
    }

    if (request.EmpresaId <= 0)
    {
        return Results.BadRequest(new { message = "Selecione a empresa contratante." });
    }

    var email = request.Email.Trim().ToLowerInvariant();
    if (!EmailValido(email))
    {
        return Results.BadRequest(new { message = "Informe um e-mail valido." });
    }

    if (!PasswordService.IsStrong(request.Senha))
    {
        return Results.BadRequest(new { message = "A senha deve ter no minimo 8 caracteres, letra maiuscula, numero e caractere especial." });
    }

    var empresasPermitidas = IsSaas(auth) ? null : await EmpresasPermitidas(db, auth);
    if (empresasPermitidas is not null && !empresasPermitidas.Contains(request.EmpresaId))
    {
        return Results.Forbid();
    }

    var empresaExiste = await db.Empresas.AnyAsync(empresa => empresa.Id == request.EmpresaId);
    if (!empresaExiste)
    {
        return Results.BadRequest(new { message = "Empresa contratante nao encontrada." });
    }

    var emailExiste = await db.Users.AnyAsync(user => user.Email == email);
    if (emailExiste)
    {
        return Results.Conflict(new { message = "Ja existe um usuario com este e-mail." });
    }

    var role = request.Role is "Administrador" or "Atendente" or "Usuario" ? request.Role : "Usuario";
    var usuario = new Users
    {
        EmpresaId = request.EmpresaId,
        Nome = request.Nome.Trim(),
        Login = email,
        Email = email,
        SenhaHash = PasswordService.Hash(request.Senha),
        Role = role,
        Ativo = true
    };

    db.Users.Add(usuario);
    await db.SaveChangesAsync();

    return Results.Created($"/api/usuarios/{usuario.Id}", new UsuarioResponse
    {
        Id = usuario.Id,
        EmpresaId = usuario.EmpresaId,
        Nome = usuario.Nome,
        Email = usuario.Email,
        Role = usuario.Role,
        Ativo = usuario.Ativo
    });
});

app.MapPut("/api/usuarios/{id:int}", async (AppDbContext db, HttpContext http, int id, AtualizarUsuarioRequest request) =>
{
    var auth = Auth(http);
    if (!IsSaas(auth) && !IsCompanyAdmin(auth) && auth.UserId != id)
    {
        return Results.Forbid();
    }

    var usuario = await db.Users
        .Include(user => user.Empresa)
        .FirstOrDefaultAsync(user => user.Id == id);

    if (usuario is null)
    {
        return Results.NotFound();
    }

    if (!IsSaas(auth) && auth.UserId != id)
    {
        var empresasPermitidas = await EmpresasPermitidas(db, auth);
        if (!usuario.EmpresaId.HasValue || !empresasPermitidas.Contains(usuario.EmpresaId.Value) || !empresasPermitidas.Contains(request.EmpresaId))
        {
            return Results.Forbid();
        }
    }

    if (!IsSaas(auth) && auth.UserId == id)
    {
        request.EmpresaId = usuario.EmpresaId ?? 0;
        request.Role = usuario.Role;
        request.Ativo = usuario.Ativo;
    }

    if (usuario.Role == "AdministradorSaas")
    {
        usuario.EmpresaId = null;
    }
    else
    {
        var empresaExiste = await db.Empresas.AnyAsync(empresa => empresa.Id == request.EmpresaId);
        if (!empresaExiste)
        {
            return Results.BadRequest(new { message = "Empresa contratante nao encontrada." });
        }

        usuario.EmpresaId = request.EmpresaId;
    }

    if (!string.IsNullOrWhiteSpace(request.Senha))
    {
        if (!PasswordService.IsStrong(request.Senha))
        {
            return Results.BadRequest(new { message = "A senha deve ter no minimo 8 caracteres, letra maiuscula, numero e caractere especial." });
        }

        usuario.SenhaHash = PasswordService.Hash(request.Senha);
    }

    usuario.Nome = request.Nome.Trim();
    usuario.Role = usuario.Role == "AdministradorSaas"
        ? "AdministradorSaas"
        : request.Role is "Administrador" or "Atendente" or "Usuario" ? request.Role : "Usuario";
    usuario.Ativo = request.Ativo;

    await db.SaveChangesAsync();
    await db.Entry(usuario).Reference(user => user.Empresa).LoadAsync();

    return Results.Ok(new UsuarioResponse
    {
        Id = usuario.Id,
        EmpresaId = usuario.EmpresaId,
        EmpresaNome = usuario.Empresa != null ? usuario.Empresa.Nome : "SaaS",
        Nome = usuario.Nome,
        Email = usuario.Email,
        Role = usuario.Role,
        Ativo = usuario.Ativo
    });
});

app.MapPut("/api/usuarios/{id:int}/senha", async (AppDbContext db, HttpContext http, int id, AlterarSenhaRequest request) =>
{
    var auth = Auth(http);
    if (auth.UserId != id && !IsSaas(auth))
    {
        return Results.Forbid();
    }

    var usuario = await db.Users.FindAsync(id);
    if (usuario is null)
    {
        return Results.NotFound();
    }

    if (!PasswordService.Verify(request.SenhaAtual, usuario.SenhaHash))
    {
        return Results.Unauthorized();
    }

    if (!PasswordService.IsStrong(request.NovaSenha))
    {
        return Results.BadRequest(new { message = "A senha deve ter no minimo 8 caracteres, letra maiuscula, numero e caractere especial." });
    }

    usuario.SenhaHash = PasswordService.Hash(request.NovaSenha);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.MapGet("/api/chamados", async (AppDbContext db, HttpContext http) =>
{
    var auth = Auth(http);
    var query = db.Chamados
        .Include(chamado => chamado.Anexos)
        .Include(chamado => chamado.Comunicacoes.OrderBy(mensagem => mensagem.EnviadoEm).ThenBy(mensagem => mensagem.Id))
        .AsQueryable();

    if (!IsSaas(auth))
    {
        if (!auth.EmpresaId.HasValue)
        {
            return Results.Forbid();
        }

        query = query.Where(chamado => chamado.EmpresaId == auth.EmpresaId);
    }

    if (IsAtendente(auth))
    {
        query = query.Where(chamado => chamado.Status == StatusChamado.Aberto || chamado.AtendenteId == auth.UserId);
    }
    else if (IsUsuario(auth))
    {
        query = query.Where(chamado =>
            chamado.SolicitanteUsuarioId == auth.UserId
            || chamado.Solicitante == auth.Nome
            || chamado.Solicitante == auth.Email);
    }

    var chamados = await query
        .OrderByDescending(chamado => chamado.CriadoEm)
        .ToListAsync();

    return Results.Ok(chamados);
});

app.MapGet("/api/chamados/{id:int}", async (AppDbContext db, HttpContext http, int id) =>
{
    var auth = Auth(http);
    var chamado = await db.Chamados
        .Include(item => item.Anexos)
        .Include(item => item.Comunicacoes.OrderBy(mensagem => mensagem.EnviadoEm))
        .FirstOrDefaultAsync(item => item.Id == id);

    if (chamado is null)
    {
        return Results.NotFound();
    }

    return PodeAcessarChamado(auth, chamado) ? Results.Ok(chamado) : Results.Forbid();
});

app.MapGet("/api/categorias-chamado", async (AppDbContext db, HttpContext http, int? empresaId) =>
{
    var auth = Auth(http);
    var query = db.CategoriasChamados.AsQueryable();

    if (IsSaas(auth) && empresaId.HasValue)
    {
        query = query.Where(categoria => categoria.EmpresaId == empresaId);
    }
    else if (!IsSaas(auth))
    {
        query = query.Where(categoria => categoria.EmpresaId == auth.EmpresaId);
    }

    var categorias = await query
        .OrderBy(categoria => categoria.Nome)
        .ToListAsync();

    return Results.Ok(categorias);
});

app.MapPost("/api/categorias-chamado", async (AppDbContext db, HttpContext http, CategoriaChamado categoria) =>
{
    var auth = Auth(http);
    if (!IsSaas(auth) && !IsCompanyAdmin(auth))
    {
        return Results.Forbid();
    }

    categoria.EmpresaId = IsSaas(auth) ? categoria.EmpresaId : auth.EmpresaId;
    if (categoria.EmpresaId.HasValue)
    {
        var empresaExiste = await db.Empresas.AnyAsync(empresa => empresa.Id == categoria.EmpresaId.Value);
        if (!empresaExiste)
        {
            return Results.BadRequest(new { message = "Empresa vinculada a categoria nao encontrada." });
        }
    }

    categoria.Nome = categoria.Nome.Trim();
    categoria.Subcategorias = categoria.Subcategorias.Trim();
    categoria.CriadoEm = DateTime.UtcNow;
    categoria.AtualizadoEm = DateTime.UtcNow;

    db.CategoriasChamados.Add(categoria);
    await db.SaveChangesAsync();

    return Results.Created($"/api/categorias-chamado/{categoria.Id}", categoria);
});

app.MapPut("/api/categorias-chamado/{id:int}", async (AppDbContext db, HttpContext http, int id, CategoriaChamado request) =>
{
    var auth = Auth(http);
    if (!IsSaas(auth) && !IsCompanyAdmin(auth))
    {
        return Results.Forbid();
    }

    var categoria = await db.CategoriasChamados.FindAsync(id);
    if (categoria is null)
    {
        return Results.NotFound();
    }

    if (!IsSaas(auth) && categoria.EmpresaId != auth.EmpresaId)
    {
        return Results.Forbid();
    }

    categoria.Nome = request.Nome.Trim();
    categoria.Subcategorias = request.Subcategorias.Trim();
    categoria.PrioridadePadrao = request.PrioridadePadrao;
    categoria.Ativo = request.Ativo;
    categoria.AtualizadoEm = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(categoria);
});

app.MapPost("/api/chamados", async (AppDbContext db, HttpContext http, CriarChamadoRequest request) =>
{
    var auth = Auth(http);
    request.EmpresaId = IsSaas(auth) ? request.EmpresaId : auth.EmpresaId;
    request.SolicitanteUsuarioId = auth.UserId;
    request.Solicitante = auth.Nome;

    if (request.EmpresaId.HasValue)
    {
        var empresaExiste = await db.Empresas.AnyAsync(empresa => empresa.Id == request.EmpresaId.Value);
        if (!empresaExiste)
        {
            return Results.BadRequest(new { message = "Empresa vinculada ao chamado nao encontrada." });
        }
    }

    var chamado = new Chamado
    {
        EmpresaId = request.EmpresaId,
        SolicitanteUsuarioId = request.SolicitanteUsuarioId,
        Numero = await GerarNumeroChamado(db),
        Titulo = request.Titulo,
        Solicitante = request.Solicitante,
        Categoria = request.Categoria,
        Subcategoria = request.Subcategoria,
        Tipo = request.Tipo,
        Prioridade = request.Prioridade,
        Status = StatusChamado.Aberto,
        Descricao = request.Descricao,
        EquipamentoRelacionado = request.EquipamentoRelacionado,
        Anexos = request.Anexos
    };

    db.Chamados.Add(chamado);
    await db.SaveChangesAsync();

    return Results.Created($"/api/chamados/{chamado.Id}", chamado);
});

app.MapPut("/api/chamados/{id:int}", async (AppDbContext db, HttpContext http, int id, EditarChamadoRequest request) =>
{
    var auth = Auth(http);
    var chamado = await db.Chamados.FindAsync(id);
    if (chamado is null)
    {
        return Results.NotFound();
    }

    if (!PodeAcessarChamado(auth, chamado))
    {
        return Results.Forbid();
    }

    if (IsUsuario(auth))
    {
        if (!UsuarioEhSolicitanteChamado(auth, chamado) || chamado.Status is StatusChamado.Encerrado or StatusChamado.Cancelado)
        {
            return Results.Forbid();
        }

        chamado.Titulo = request.Titulo.Trim();
        chamado.Descricao = request.Descricao.Trim();
        chamado.AtualizadoEm = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return Results.Ok(chamado);
    }

    chamado.Titulo = request.Titulo.Trim();
    chamado.Categoria = request.Categoria.Trim();
    chamado.Subcategoria = request.Subcategoria.Trim();
    chamado.Tipo = request.Tipo;
    chamado.Prioridade = request.Prioridade;
    chamado.Status = request.Status;
    chamado.Descricao = request.Descricao.Trim();
    chamado.AtualizadoEm = DateTime.UtcNow;
    chamado.EncerradoEm = request.Status is StatusChamado.Encerrado or StatusChamado.Cancelado ? DateTime.UtcNow : null;

    await db.SaveChangesAsync();
    return Results.Ok(chamado);
});

app.MapPost("/api/chamados/{id:int}/capturar", async (AppDbContext db, HttpContext http, int id) =>
{
    var auth = Auth(http);
    if (!IsSaas(auth) && !IsCompanyAdmin(auth) && !IsAtendente(auth))
    {
        return Results.Forbid();
    }

    var chamado = await db.Chamados.FindAsync(id);
    if (chamado is null)
    {
        return Results.NotFound();
    }

    if (!PodeAcessarChamado(auth, chamado))
    {
        return Results.Forbid();
    }

    chamado.AtendenteId = auth.UserId;
    chamado.AtendenteNome = auth.Nome;
    chamado.Status = StatusChamado.EmAtendimento;
    chamado.AtualizadoEm = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(chamado);
});

app.MapPost("/api/chamados/{id:int}/mensagens", async (AppDbContext db, HttpContext http, int id, CriarMensagemRequest request) =>
{
    var auth = Auth(http);
    var chamado = await db.Chamados.FindAsync(id);
    if (chamado is null)
    {
        return Results.NotFound();
    }

    if (!PodeAcessarChamado(auth, chamado))
    {
        return Results.Forbid();
    }

    var mensagem = new ComunicacaoChamado
    {
        ChamadoId = id,
        AutorId = auth.UserId,
        AutorNome = auth.Nome,
        AutorPerfil = auth.Role,
        Mensagem = request.Mensagem.Trim(),
        Tipo = request.Tipo == "Mural" ? "Mural" : "Chat"
    };

    db.ComunicacoesChamados.Add(mensagem);
    await db.Chamados
        .Where(item => item.Id == id)
        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.AtualizadoEm, DateTime.UtcNow));
    await db.SaveChangesAsync();

    return Results.Created($"/api/chamados/{id}/mensagens/{mensagem.Id}", mensagem);
});

app.MapPost("/api/chamados/{id:int}/mensagens/lidas", async (AppDbContext db, HttpContext http, int id) =>
{
    var auth = Auth(http);
    var chamado = await db.Chamados.FindAsync(id);
    if (chamado is null)
    {
        return Results.NotFound();
    }

    if (!PodeAcessarChamado(auth, chamado))
    {
        return Results.Forbid();
    }

    await db.ComunicacoesChamados
        .Where(item => item.ChamadoId == id && !item.Lida)
        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Lida, true));

    return Results.NoContent();
});

app.MapPost("/api/chamados/{id:int}/anexos", async (AppDbContext db, HttpContext http, int id, List<AnexoChamado> request) =>
{
    var auth = Auth(http);
    var chamado = await db.Chamados
        .Include(item => item.Anexos)
        .FirstOrDefaultAsync(item => item.Id == id);

    if (chamado is null)
    {
        return Results.NotFound();
    }

    if (!PodeAcessarChamado(auth, chamado))
    {
        return Results.Forbid();
    }

    if (chamado.Status is StatusChamado.Encerrado or StatusChamado.Cancelado)
    {
        return Results.BadRequest(new { message = "Tickets encerrados nao recebem novos anexos." });
    }

    var anexos = request
        .Where(anexo => !string.IsNullOrWhiteSpace(anexo.NomeArquivo))
        .Select(anexo => new AnexoChamado
        {
            NomeArquivo = anexo.NomeArquivo.Trim(),
            TipoConteudo = anexo.TipoConteudo.Trim(),
            TamanhoBytes = anexo.TamanhoBytes,
            Url = anexo.Url.Trim(),
            EnviadoEm = DateTime.UtcNow
        })
        .ToList();

    if (anexos.Count == 0)
    {
        return Results.BadRequest(new { message = "Nenhum anexo valido enviado." });
    }

    chamado.Anexos.AddRange(anexos);
    chamado.AtualizadoEm = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(chamado.Anexos.OrderBy(anexo => anexo.EnviadoEm));
});

app.MapDelete("/api/chamados/{id:int}/anexos/{anexoId:int}", async (AppDbContext db, HttpContext http, int id, int anexoId) =>
{
    var auth = Auth(http);
    var chamado = await db.Chamados
        .Include(item => item.Anexos)
        .FirstOrDefaultAsync(item => item.Id == id);

    if (chamado is null)
    {
        return Results.NotFound();
    }

    if (!PodeAcessarChamado(auth, chamado))
    {
        return Results.Forbid();
    }

    var anexo = chamado.Anexos.FirstOrDefault(item => item.Id == anexoId);
    if (anexo is null)
    {
        return Results.NotFound();
    }

    db.AnexosChamados.Remove(anexo);
    chamado.AtualizadoEm = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.MapPost("/api/chamados/{id:int}/digitando", async (AppDbContext db, HttpContext http, int id) =>
{
    var auth = Auth(http);
    var chamado = await db.Chamados.FindAsync(id);
    if (chamado is null)
    {
        return Results.NotFound();
    }

    if (!PodeAcessarChamado(auth, chamado))
    {
        return Results.Forbid();
    }

    var key = $"{id}:{auth.UserId}";
    ticketTypingStates[key] = new TypingState(id, auth.UserId, auth.Nome, DateTime.UtcNow.AddSeconds(3));
    return Results.NoContent();
});

app.MapGet("/api/chamados/{id:int}/digitando", async (AppDbContext db, HttpContext http, int id) =>
{
    var auth = Auth(http);
    var chamado = await db.Chamados.FindAsync(id);
    if (chamado is null)
    {
        return Results.NotFound();
    }

    if (!PodeAcessarChamado(auth, chamado))
    {
        return Results.Forbid();
    }

    var agora = DateTime.UtcNow;
    foreach (var item in ticketTypingStates)
    {
        if (item.Value.ExpiraEm <= agora)
        {
            ticketTypingStates.TryRemove(item.Key, out _);
        }
    }

    var digitando = ticketTypingStates.Values
        .Where(item => item.ChamadoId == id && item.UsuarioId != auth.UserId && item.ExpiraEm > agora)
        .Select(item => item.UsuarioNome)
        .Distinct()
        .ToList();

    return Results.Ok(digitando);
});

app.MapPost("/api/chamados/{id:int}/encerrar", async (AppDbContext db, HttpContext http, int id) => await AtualizarStatusChamado(db, Auth(http), id, StatusChamado.Encerrado));
app.MapPost("/api/chamados/{id:int}/cancelar", async (AppDbContext db, HttpContext http, int id) => await AtualizarStatusChamado(db, Auth(http), id, StatusChamado.Cancelado));
app.MapPost("/api/chamados/{id:int}/reabrir", async (AppDbContext db, HttpContext http, int id) =>
{
    var auth = Auth(http);
    var chamado = await db.Chamados.FindAsync(id);
    if (chamado is null)
    {
        return Results.NotFound();
    }

    if (!IsUsuario(auth) || !PodeAcessarChamado(auth, chamado) || !UsuarioEhSolicitanteChamado(auth, chamado) || chamado.Status != StatusChamado.Encerrado)
    {
        return Results.Forbid();
    }

    chamado.Status = StatusChamado.Aberto;
    chamado.AtualizadoEm = DateTime.UtcNow;
    chamado.EncerradoEm = null;

    await db.SaveChangesAsync();
    return Results.Ok(chamado);
});

app.MapPost("/api/chamados/{id:int}/avaliar", async (AppDbContext db, HttpContext http, int id, AvaliarChamadoRequest request) =>
{
    var auth = Auth(http);
    var chamado = await db.Chamados.FindAsync(id);
    if (chamado is null)
    {
        return Results.NotFound();
    }

    if (!PodeAcessarChamado(auth, chamado)
        || chamado.Status != StatusChamado.Encerrado
        || !UsuarioEhSolicitanteChamado(auth, chamado))
    {
        return Results.Forbid();
    }

    chamado.AvaliacaoNota = Math.Clamp(request.Nota, 1, 5);
    chamado.AvaliacaoComentario = request.Comentario?.Trim();
    chamado.AtualizadoEm = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(chamado);
});

app.MapGet("/api/categorias-conhecimento", async (AppDbContext db, HttpContext http, int? empresaId) =>
{
    var auth = Auth(http);
    var query = db.CategoriasConhecimento.AsQueryable();

    if (IsSaas(auth) && empresaId.HasValue)
    {
        query = query.Where(item => item.EmpresaId == empresaId || item.EmpresaId == null);
    }
    else if (!IsSaas(auth))
    {
        query = query.Where(item => item.EmpresaId == auth.EmpresaId || item.EmpresaId == null);
    }

    var categorias = await query
        .OrderBy(item => item.Nome)
        .ToListAsync();

    return Results.Ok(categorias);
});

app.MapPost("/api/categorias-conhecimento", async (AppDbContext db, HttpContext http, CategoriaConhecimento categoria) =>
{
    var auth = Auth(http);
    if (!IsSaas(auth) && !IsCompanyAdmin(auth) && !IsAtendente(auth))
    {
        return Results.Forbid();
    }

    categoria.EmpresaId = IsSaas(auth) ? categoria.EmpresaId : auth.EmpresaId;
    categoria.Nome = categoria.Nome.Trim();
    categoria.CriadoEm = DateTime.UtcNow;

    var existente = await db.CategoriasConhecimento
        .FirstOrDefaultAsync(item => item.EmpresaId == categoria.EmpresaId && item.Nome.ToLower() == categoria.Nome.ToLower());

    if (existente is not null)
    {
        existente.Ativo = categoria.Ativo;
        await db.SaveChangesAsync();
        return Results.Ok(existente);
    }

    db.CategoriasConhecimento.Add(categoria);
    await db.SaveChangesAsync();

    return Results.Created($"/api/categorias-conhecimento/{categoria.Id}", categoria);
});

app.MapPut("/api/categorias-conhecimento/{id:int}", async (AppDbContext db, HttpContext http, int id, CategoriaConhecimento request) =>
{
    var auth = Auth(http);
    if (!IsSaas(auth) && !IsCompanyAdmin(auth) && !IsAtendente(auth))
    {
        return Results.Forbid();
    }

    var categoria = await db.CategoriasConhecimento.FindAsync(id);

    if (categoria is null)
    {
        return Results.NotFound();
    }

    if (!IsSaas(auth) && categoria.EmpresaId != auth.EmpresaId)
    {
        return Results.Forbid();
    }

    var nomeAnterior = categoria.Nome;
    categoria.Nome = request.Nome.Trim();
    categoria.Ativo = request.Ativo;

    if (!string.Equals(nomeAnterior, categoria.Nome, StringComparison.OrdinalIgnoreCase))
    {
        var artigos = await db.BaseConhecimento
            .Where(item => item.EmpresaId == categoria.EmpresaId && item.Categoria == nomeAnterior)
            .ToListAsync();

        foreach (var artigo in artigos)
        {
            artigo.Categoria = categoria.Nome;
            artigo.AtualizadoEm = DateTime.UtcNow;
        }
    }

    await db.SaveChangesAsync();
    return Results.Ok(categoria);
});

app.MapDelete("/api/categorias-conhecimento/{id:int}", async (AppDbContext db, HttpContext http, int id) =>
{
    var auth = Auth(http);
    if (!IsSaas(auth) && !IsCompanyAdmin(auth) && !IsAtendente(auth))
    {
        return Results.Forbid();
    }

    var categoria = await db.CategoriasConhecimento.FindAsync(id);

    if (categoria is null)
    {
        return Results.NotFound();
    }

    if (!IsSaas(auth) && categoria.EmpresaId != auth.EmpresaId)
    {
        return Results.Forbid();
    }

    var possuiConhecimento = await db.BaseConhecimento
        .AnyAsync(item => item.Publicado && item.EmpresaId == categoria.EmpresaId && item.Categoria == categoria.Nome);

    if (possuiConhecimento)
    {
        return Results.Conflict(new { mensagem = "Categoria possui conhecimentos cadastrados." });
    }

    db.CategoriasConhecimento.Remove(categoria);
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.MapGet("/api/base-conhecimento", async (AppDbContext db, HttpContext http, int? empresaId) =>
{
    var auth = Auth(http);
    var query = db.BaseConhecimento.AsQueryable();

    if (IsUsuario(auth))
    {
        query = query.Where(item => item.Status == "Publicado" && item.Publicado);
    }
    else if (IsAtendente(auth))
    {
        query = query.Where(item => item.Status != "Arquivado" && (item.Status == "Publicado" || item.UsuarioCriadorId == auth.UserId));
    }

    if (IsSaas(auth) && empresaId.HasValue)
    {
        query = query.Where(item => item.EmpresaId == empresaId || item.EmpresaId == null);
    }
    else if (!IsSaas(auth))
    {
        query = query.Where(item => item.EmpresaId == auth.EmpresaId || item.EmpresaId == null);
    }

    var artigos = await query
        .OrderBy(item => item.Categoria)
        .ThenBy(item => item.Titulo)
        .ToListAsync();

    return Results.Ok(artigos);
});

app.MapPost("/api/base-conhecimento", async (AppDbContext db, HttpContext http, ArtigoConhecimento artigo) =>
{
    var auth = Auth(http);
    if (!IsSaas(auth) && !IsCompanyAdmin(auth) && !IsAtendente(auth))
    {
        return Results.Forbid();
    }

    artigo.EmpresaId = IsSaas(auth) ? artigo.EmpresaId : auth.EmpresaId;
    artigo.UsuarioCriadorId = auth.UserId;
    artigo.UsuarioCriador = auth.Nome;
    artigo.Titulo = artigo.Titulo.Trim();
    artigo.Categoria = artigo.Categoria.Trim();
    artigo.Status = NormalizarStatusConhecimento(artigo.Status);
    artigo.AtualizadoEm = DateTime.UtcNow;
    db.BaseConhecimento.Add(artigo);
    await db.SaveChangesAsync();

    return Results.Created($"/api/base-conhecimento/{artigo.Id}", artigo);
});

app.MapPut("/api/base-conhecimento/{id:int}", async (AppDbContext db, HttpContext http, int id, ArtigoConhecimento request) =>
{
    var auth = Auth(http);
    if (!IsSaas(auth) && !IsCompanyAdmin(auth) && !IsAtendente(auth))
    {
        return Results.Forbid();
    }

    var artigo = await db.BaseConhecimento.FindAsync(id);

    if (artigo is null)
    {
        return Results.NotFound();
    }

    if (!IsSaas(auth) && artigo.EmpresaId != auth.EmpresaId)
    {
        return Results.Forbid();
    }

    artigo.EmpresaId = IsSaas(auth) ? request.EmpresaId : auth.EmpresaId;
    artigo.Titulo = request.Titulo.Trim();
    artigo.Categoria = request.Categoria.Trim();
    artigo.Conteudo = request.Conteudo;
    artigo.Tags = request.Tags;
    artigo.Anexos = request.Anexos;
    artigo.UsuarioCriador = request.UsuarioCriador;
    artigo.UsuarioCriadorId = request.UsuarioCriadorId;
    artigo.Publicado = request.Publicado;
    artigo.Status = NormalizarStatusConhecimento(request.Status);
    artigo.AtualizadoEm = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(artigo);
});

app.MapDelete("/api/base-conhecimento/{id:int}", async (AppDbContext db, HttpContext http, int id) =>
{
    var auth = Auth(http);
    if (!IsSaas(auth) && !IsCompanyAdmin(auth) && !IsAtendente(auth))
    {
        return Results.Forbid();
    }

    var artigo = await db.BaseConhecimento.FindAsync(id);

    if (artigo is null)
    {
        return Results.NotFound();
    }

    if (!IsSaas(auth) && artigo.EmpresaId != auth.EmpresaId)
    {
        return Results.Forbid();
    }

    artigo.Publicado = false;
    artigo.AtualizadoEm = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.MapGet("/api/equipamentos/envios", async (AppDbContext db, HttpContext http, int? empresaId) =>
{
    var auth = Auth(http);
    var query = db.ControleEquipamentos.AsQueryable();

    if (IsSaas(auth) && empresaId.HasValue)
    {
        var empresasPermitidas = await EmpresasDaContratante(db, empresaId.Value);
        query = query.Where(item => item.EmpresaId.HasValue && empresasPermitidas.Contains(item.EmpresaId.Value));
    }
    else if (!IsSaas(auth))
    {
        var empresasPermitidas = await EmpresasPermitidas(db, auth);
        query = query.Where(item => item.EmpresaId.HasValue && empresasPermitidas.Contains(item.EmpresaId.Value));
    }

    var envios = await query
        .OrderByDescending(item => item.CriadoEm)
        .ToListAsync();

    return Results.Ok(envios);
});

app.MapPost("/api/equipamentos/envios", async (AppDbContext db, HttpContext http, ControleEquipamento envio) =>
{
    var auth = Auth(http);
    if (IsUsuario(auth))
    {
        return Results.Forbid();
    }

    envio.EmpresaId = IsSaas(auth) ? envio.EmpresaId : auth.EmpresaId;
    envio.CriadoEm = DateTime.UtcNow;
    db.ControleEquipamentos.Add(envio);
    await db.SaveChangesAsync();

    return Results.Created($"/api/equipamentos/envios/{envio.Id}", envio);
});

app.MapGet("/api/equipamentos/inventario", async (AppDbContext db, HttpContext http, int? empresaId) =>
{
    var auth = Auth(http);
    var query = db.InventarioEquipamentos
        .Include(item => item.Empresa)
        .AsQueryable();

    if (IsSaas(auth) && empresaId.HasValue)
    {
        var empresasPermitidas = await EmpresasDaContratante(db, empresaId.Value);
        query = query.Where(item => item.EmpresaId.HasValue && empresasPermitidas.Contains(item.EmpresaId.Value));
    }
    else if (!IsSaas(auth))
    {
        var empresasPermitidas = await EmpresasPermitidas(db, auth);
        query = query.Where(item => item.EmpresaId.HasValue && empresasPermitidas.Contains(item.EmpresaId.Value));
    }

    var inventario = await query
        .OrderBy(item => item.Unidade)
        .ThenBy(item => item.Hostname)
        .ToListAsync();

    return Results.Ok(inventario);
});

app.MapGet("/api/equipamentos/publico/{patrimonio}", async (AppDbContext db, string patrimonio) =>
{
    var equipamento = await db.InventarioEquipamentos
        .AsNoTracking()
        .Include(item => item.Empresa)
        .FirstOrDefaultAsync(item => item.Patrimonio == patrimonio);

    return equipamento is null ? Results.NotFound() : Results.Ok(equipamento);
});

app.MapGet("/api/equipamentos/publico/{patrimonio}/pagina", async (AppDbContext db, string patrimonio) =>
{
    var equipamento = await db.InventarioEquipamentos
        .AsNoTracking()
        .Include(item => item.Empresa)
        .FirstOrDefaultAsync(item => item.Patrimonio == patrimonio);

    if (equipamento is null)
    {
        return Results.NotFound();
    }

    var status = equipamento.Online ? "Online" : "Offline";
    var cor = equipamento.Online ? "#16a34a" : "#dc2626";
    var html = $$"""
<!doctype html>
<html lang="pt-BR">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width,initial-scale=1">
  <title>{{System.Net.WebUtility.HtmlEncode(equipamento.Hostname)}}</title>
  <style>
    body{margin:0;font-family:Inter,Arial,sans-serif;background:#f6f8fb;color:#0f172a}
    main{max-width:520px;margin:0 auto;padding:28px 18px}
    section{border:1px solid #d8e2ef;border-radius:14px;background:#fff;padding:22px;box-shadow:0 18px 42px #0f172a14}
    small{text-transform:uppercase;color:#64748b;font-weight:800;letter-spacing:.04em}
    h1{margin:6px 0 4px;font-size:1.7rem}.status{display:inline-flex;gap:8px;align-items:center;margin:12px 0;padding:8px 12px;border-radius:999px;background:#f8fafc;font-weight:900}
    .dot{width:10px;height:10px;border-radius:999px;background:{{cor}};box-shadow:0 0 16px {{cor}}66}
    dl{display:grid;gap:12px;margin:18px 0 0}div{border-top:1px solid #e2e8f0;padding-top:10px}dt{color:#64748b;font-size:.72rem;font-weight:900;text-transform:uppercase}dd{margin:4px 0 0;font-weight:800}
  </style>
</head>
<body>
<main>
<section>
<small>Equipamento UniFlowIT</small>
<h1>{{System.Net.WebUtility.HtmlEncode(equipamento.Hostname)}}</h1>
<span class="status"><span class="dot"></span>{{status}}</span>
<dl>
<div><dt>ID</dt><dd>{{System.Net.WebUtility.HtmlEncode(equipamento.Patrimonio)}}</dd></div>
<div><dt>Tipo</dt><dd>{{System.Net.WebUtility.HtmlEncode(equipamento.Tipo)}}</dd></div>
<div><dt>Marca / Modelo</dt><dd>{{System.Net.WebUtility.HtmlEncode($"{equipamento.Marca} {equipamento.Modelo}".Trim())}}</dd></div>
<div><dt>Responsavel</dt><dd>{{System.Net.WebUtility.HtmlEncode(equipamento.Responsavel)}}</dd></div>
<div><dt>Unidade</dt><dd>{{System.Net.WebUtility.HtmlEncode(equipamento.Unidade)}}</dd></div>
<div><dt>Descricao</dt><dd>{{System.Net.WebUtility.HtmlEncode(equipamento.Descricao)}}</dd></div>
</dl>
</section>
</main>
</body>
</html>
""";

    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapGet("/api/agent/installer/windows", (HttpContext http, IWebHostEnvironment env) =>
{
    var installerPath = FindAgentInstaller(env.ContentRootPath);
    if (installerPath is not null)
    {
        return Results.File(
            File.ReadAllBytes(installerPath),
            "application/octet-stream",
            "UniFlowIT-Agent-Setup-1.0.9.exe");
    }

    var apiBase = $"{http.Request.Scheme}://{http.Request.Host}/api";
    var script = $$"""
param(
  [string]$ApiBase = "{{apiBase}}",
  [string]$InstallDir = "$env:LOCALAPPDATA\UniFlowIT\Agent"
)

$ErrorActionPreference = "Stop"
$zipPath = Join-Path $env:TEMP "UniFlowIT.Agent.zip"
$downloadUrl = "$ApiBase/agent/download/windows"

Write-Host "Baixando UniFlowIT Agent..."
Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath -UseBasicParsing

if (Test-Path $InstallDir) {
  Remove-Item -LiteralPath $InstallDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Expand-Archive -LiteralPath $zipPath -DestinationPath $InstallDir -Force

$installedExe = Join-Path $InstallDir "UniFlowIT.Agent.exe"
if (!(Test-Path $installedExe)) {
  throw "UniFlowIT.Agent.exe nao foi encontrado apos a instalacao."
}

$uninstallScript = Join-Path $InstallDir "uninstall-agent.ps1"
$uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\UniFlowIT Agent"

@"
`$ErrorActionPreference = "SilentlyContinue"
`$installDir = "$InstallDir"

Get-Process | Where-Object { `$_.ProcessName -eq "UniFlowIT.Agent" } | Stop-Process -Force
Get-Process | Where-Object { `$_.ProcessName -eq "rustdesk" } | Stop-Process -Force
Get-Service | Where-Object { `$_.Name -like "*RustDesk*" -or `$_.DisplayName -like "*RustDesk*" } | Stop-Service -Force
`$winget = Get-Command winget.exe -ErrorAction SilentlyContinue
if (`$winget) { & `$winget.Source uninstall --id RustDesk.RustDesk -e --silent --disable-interactivity --accept-source-agreements | Out-Null }
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "UniFlowIT Agent" -ErrorAction SilentlyContinue
Remove-Item -LiteralPath "HKCU:\Software\Classes\uniflowit-agent" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\UniFlowIT Agent" -Recurse -Force -ErrorAction SilentlyContinue
Set-Location `$env:TEMP
Remove-Item -LiteralPath `$installDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "UniFlowIT Agent desinstalado."
"@ | Set-Content -LiteralPath $uninstallScript -Encoding UTF8

New-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" -Name "UniFlowIT Agent" -Value "`"$installedExe`""

New-Item -Path "HKCU:\Software\Classes\uniflowit-agent" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Classes\uniflowit-agent" -Name "(default)" -Value "URL:UniFlowIT Agent"
Set-ItemProperty -Path "HKCU:\Software\Classes\uniflowit-agent" -Name "URL Protocol" -Value ""
New-Item -Path "HKCU:\Software\Classes\uniflowit-agent\shell\open\command" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Classes\uniflowit-agent\shell\open\command" -Name "(default)" -Value "`"$installedExe`" `"%1`""

New-Item -Path $uninstallKey -Force | Out-Null
Set-ItemProperty -Path $uninstallKey -Name "DisplayName" -Value "UniFlowIT Agent"
Set-ItemProperty -Path $uninstallKey -Name "DisplayVersion" -Value "1.0.9"
Set-ItemProperty -Path $uninstallKey -Name "Publisher" -Value "UniFlowIT"
Set-ItemProperty -Path $uninstallKey -Name "InstallLocation" -Value $InstallDir
Set-ItemProperty -Path $uninstallKey -Name "DisplayIcon" -Value $installedExe
Set-ItemProperty -Path $uninstallKey -Name "UninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -File `"$uninstallScript`""
Set-ItemProperty -Path $uninstallKey -Name "QuietUninstallString" -Value "powershell.exe -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$uninstallScript`""
Set-ItemProperty -Path $uninstallKey -Name "NoModify" -Type DWord -Value 1
Set-ItemProperty -Path $uninstallKey -Name "NoRepair" -Type DWord -Value 1

Start-Process -FilePath $installedExe -WindowStyle Hidden
Write-Host "UniFlowIT Agent instalado em $InstallDir"
Write-Host "Volte ao UniFlowIT e faca login novamente para sincronizar o equipamento."
""";

    return Results.File(
        System.Text.Encoding.UTF8.GetBytes(script),
        "application/octet-stream",
        "install-uniflowit-agent.ps1");
});

app.MapGet("/api/agent/download/windows", async (IWebHostEnvironment env) =>
{
    var packagePath = FindAgentPackage(env.ContentRootPath);
    if (packagePath is null)
    {
        return Results.NotFound(new { message = "Pacote do UniFlowIT Agent nao encontrado. Gere o pacote antes de publicar." });
    }

    return Results.File(
        await File.ReadAllBytesAsync(packagePath),
        "application/zip",
        "UniFlowIT.Agent.zip");
});

app.MapPost("/api/agent/equipamento", async (AppDbContext db, HttpContext http, AgentEquipmentRequest request) =>
{
    var auth = Auth(http);
    if (!IsUsuario(auth) || auth.EmpresaId is null)
    {
        return Results.Forbid();
    }

    var patrimonio = string.IsNullOrWhiteSpace(request.Patrimonio)
        ? Environment.MachineName
        : request.Patrimonio.Trim();
    var hostname = string.IsNullOrWhiteSpace(request.Hostname)
        ? patrimonio
        : request.Hostname.Trim();
    var agentId = request.AgentId.Trim();

    if (string.IsNullOrWhiteSpace(agentId))
    {
        return Results.BadRequest(new { message = "AgentId obrigatorio." });
    }

    var usuario = await db.Users.FirstOrDefaultAsync(user => user.Id == auth.UserId);
    if (usuario is null)
    {
        return Results.Forbid();
    }

    var empresasPermitidas = await EmpresasPermitidas(db, auth);
    var empresaUsuario = await db.Empresas.FirstOrDefaultAsync(item => item.Id == auth.EmpresaId);
    if (empresaUsuario is null || empresasPermitidas.Count == 0)
    {
        return Results.Forbid();
    }

    var empresaContratanteId = empresaUsuario.EmpresaContratanteId ?? empresaUsuario.Id;
    var unidadeEmpresaId = request.EmpresaId.HasValue && empresasPermitidas.Contains(request.EmpresaId.Value)
        ? request.EmpresaId.Value
        : auth.EmpresaId.Value;
    var unidadeEmpresa = await db.Empresas.FirstOrDefaultAsync(item => item.Id == unidadeEmpresaId);
    var equipamento = request.EquipamentoId.HasValue
        ? await db.InventarioEquipamentos.FirstOrDefaultAsync(item =>
            item.Id == request.EquipamentoId.Value
            && item.EmpresaId.HasValue
            && empresasPermitidas.Contains(item.EmpresaId.Value))
        : null;

    equipamento ??= await db.InventarioEquipamentos.FirstOrDefaultAsync(item =>
        item.EmpresaId.HasValue
        && empresasPermitidas.Contains(item.EmpresaId.Value)
        && (item.AgentId == agentId || item.Patrimonio == patrimonio || item.Hostname == hostname));
    var created = equipamento is null;

    if (equipamento is null)
    {
        equipamento = new InventarioEquipamento
        {
            EmpresaId = empresaContratanteId,
            Patrimonio = patrimonio,
            Hostname = hostname,
            Tipo = "Computador",
            ResponsavelUsuarioId = auth.UserId,
            Responsavel = auth.Nome,
            UsuarioAtual = auth.Nome,
            UnidadeEmpresaId = unidadeEmpresaId,
            Unidade = unidadeEmpresa?.NomeFantasia ?? unidadeEmpresa?.RazaoSocial ?? auth.Nome,
            Filial = unidadeEmpresa?.NomeFantasia ?? unidadeEmpresa?.RazaoSocial ?? string.Empty,
            Online = true
        };
        db.InventarioEquipamentos.Add(equipamento);
    }

    equipamento.EmpresaId = empresaContratanteId;
    equipamento.UnidadeEmpresaId = unidadeEmpresaId;
    equipamento.Unidade = unidadeEmpresa?.NomeFantasia ?? unidadeEmpresa?.RazaoSocial ?? equipamento.Unidade;
    equipamento.Filial = string.IsNullOrWhiteSpace(equipamento.Unidade) ? equipamento.Filial : equipamento.Unidade;
    equipamento.AgentId = agentId;
    equipamento.AgentVersion = request.AgentVersion.Trim();
    equipamento.Patrimonio = patrimonio;
    equipamento.Hostname = hostname;
    equipamento.Processador = request.Processador.Trim();
    equipamento.Gpu = request.Gpu.Trim();
    equipamento.MemoriaGb = Math.Max(0, request.MemoriaGb);
    equipamento.MemoriaLivreGb = Math.Max(0, request.MemoriaLivreGb);
    equipamento.DiscoGb = Math.Max(0, request.DiscoGb);
    equipamento.DiscoLivreGb = Math.Max(0, request.DiscoLivreGb);
    equipamento.DiscosJson = JsonSerializer.Serialize(
        request.Discos.Select(disco => new AgentDiskRequest
        {
            Nome = disco.Nome.Trim(),
            Unidade = disco.Unidade.Trim(),
            TotalGb = Math.Max(0, disco.TotalGb),
            LivreGb = Math.Max(0, disco.LivreGb)
        }),
        new JsonSerializerOptions(JsonSerializerDefaults.Web));
    equipamento.Ip = request.Ip.Trim();
    equipamento.SistemaOperacional = request.SistemaOperacional.Trim();
    equipamento.RustDeskId = request.RustDeskId.Trim();
    equipamento.RustDeskPassword = request.RustDeskPassword.Trim();
    equipamento.ResponsavelUsuarioId = auth.UserId;
    equipamento.Responsavel = auth.Nome;
    equipamento.UsuarioAtual = auth.Nome;
    equipamento.Online = true;
    equipamento.UltimaLeituraEm = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(new AgentEquipmentSyncResponse
    {
        Created = created,
        Equipamento = equipamento
    });
});

app.MapPost("/api/equipamentos/inventario", async (AppDbContext db, HttpContext http, InventarioEquipamento equipamento) =>
{
    var auth = Auth(http);
    if (IsUsuario(auth))
    {
        return Results.Forbid();
    }

    if (string.IsNullOrWhiteSpace(equipamento.Patrimonio) || string.IsNullOrWhiteSpace(equipamento.Hostname) || string.IsNullOrWhiteSpace(equipamento.Tipo) || string.IsNullOrWhiteSpace(equipamento.Responsavel))
    {
        return Results.BadRequest(new { message = "ID, nome, tipo e responsavel sao obrigatorios." });
    }

    if (!IsSaas(auth))
    {
        var empresasPermitidas = await EmpresasPermitidas(db, auth);
        var empresaUsuario = await db.Empresas.AsNoTracking().FirstOrDefaultAsync(item => item.Id == auth.EmpresaId);
        if (empresaUsuario is null || empresasPermitidas.Count == 0)
        {
            return Results.Forbid();
        }

        var unidadeEmpresaId = equipamento.UnidadeEmpresaId.HasValue && empresasPermitidas.Contains(equipamento.UnidadeEmpresaId.Value)
            ? equipamento.UnidadeEmpresaId.Value
            : auth.EmpresaId!.Value;
        var unidadeEmpresa = await db.Empresas.AsNoTracking().FirstOrDefaultAsync(item => item.Id == unidadeEmpresaId);
        equipamento.EmpresaId = empresaUsuario.EmpresaContratanteId ?? empresaUsuario.Id;
        equipamento.UnidadeEmpresaId = unidadeEmpresaId;
        equipamento.Unidade = unidadeEmpresa?.NomeFantasia ?? unidadeEmpresa?.RazaoSocial ?? equipamento.Unidade;
        equipamento.Filial = string.IsNullOrWhiteSpace(equipamento.Unidade) ? equipamento.Filial : equipamento.Unidade;
    }
    else if (equipamento.EmpresaId.HasValue)
    {
        var empresa = await db.Empresas.AsNoTracking().FirstOrDefaultAsync(item => item.Id == equipamento.EmpresaId.Value);
        equipamento.EmpresaId = empresa?.EmpresaContratanteId ?? empresa?.Id ?? equipamento.EmpresaId;
    }
    equipamento.Patrimonio = equipamento.Patrimonio.Trim();
    equipamento.Hostname = equipamento.Hostname.Trim();
    equipamento.Tipo = equipamento.Tipo.Trim();
    equipamento.Responsavel = equipamento.Responsavel.Trim();
    equipamento.DataCompra = equipamento.DataCompra.HasValue ? DateTime.SpecifyKind(equipamento.DataCompra.Value.Date, DateTimeKind.Utc) : null;
    equipamento.UltimaLeituraEm = DateTime.UtcNow;
    if (!IsSaas(auth))
    {
        equipamento.AgentId = string.Empty;
        equipamento.AgentVersion = string.Empty;
        equipamento.RustDeskId = string.Empty;
        equipamento.RustDeskPassword = string.Empty;
        equipamento.Ip = string.Empty;
        equipamento.SistemaOperacional = string.Empty;
        equipamento.Processador = string.Empty;
        equipamento.Gpu = string.Empty;
        equipamento.MemoriaGb = 0;
        equipamento.MemoriaLivreGb = 0;
        equipamento.DiscoGb = 0;
        equipamento.DiscoLivreGb = 0;
        equipamento.DiscosJson = string.Empty;
    }
    if (string.IsNullOrWhiteSpace(equipamento.UsuarioAtual))
    {
        equipamento.UsuarioAtual = equipamento.Responsavel;
    }
    if (string.IsNullOrWhiteSpace(equipamento.Filial))
    {
        equipamento.Filial = equipamento.Unidade;
    }
    if (await db.InventarioEquipamentos.AnyAsync(item => item.EmpresaId == equipamento.EmpresaId && item.Patrimonio == equipamento.Patrimonio))
    {
        return Results.Conflict(new { message = "Ja existe equipamento cadastrado com este ID." });
    }

    equipamento.UltimaLeituraEm = DateTime.UtcNow;
    db.InventarioEquipamentos.Add(equipamento);
    await db.SaveChangesAsync();

    return Results.Created($"/api/equipamentos/inventario/{equipamento.Id}", equipamento);
});

app.MapPut("/api/equipamentos/inventario/{id:int}", async (AppDbContext db, HttpContext http, int id, InventarioEquipamento request) =>
{
    var auth = Auth(http);
    if (IsUsuario(auth))
    {
        return Results.Forbid();
    }

    var equipamento = await db.InventarioEquipamentos.FirstOrDefaultAsync(item => item.Id == id);
    if (equipamento is null)
    {
        return Results.NotFound();
    }
    var empresasPermitidas = IsSaas(auth) ? new List<int>() : await EmpresasPermitidas(db, auth);
    if (!IsSaas(auth) && (!equipamento.EmpresaId.HasValue || !empresasPermitidas.Contains(equipamento.EmpresaId.Value)))
    {
        return Results.Forbid();
    }
    if (string.IsNullOrWhiteSpace(request.Patrimonio) || string.IsNullOrWhiteSpace(request.Hostname) || string.IsNullOrWhiteSpace(request.Tipo) || string.IsNullOrWhiteSpace(request.Responsavel))
    {
        return Results.BadRequest(new { message = "ID, nome, tipo e responsavel sao obrigatorios." });
    }

    equipamento.Patrimonio = request.Patrimonio.Trim();
    equipamento.Hostname = request.Hostname.Trim();
    equipamento.Marca = request.Marca.Trim();
    equipamento.Modelo = request.Modelo.Trim();
    equipamento.Tipo = request.Tipo.Trim();
    equipamento.Descricao = request.Descricao.Trim();
    equipamento.DataCompra = request.DataCompra.HasValue ? DateTime.SpecifyKind(request.DataCompra.Value.Date, DateTimeKind.Utc) : null;
    equipamento.NumeroNotaFiscal = request.NumeroNotaFiscal.Trim();
    equipamento.NotaFiscalNome = request.NotaFiscalNome.Trim();
    equipamento.NotaFiscalUrl = request.NotaFiscalUrl;
    equipamento.ResponsavelUsuarioId = request.ResponsavelUsuarioId;
    equipamento.Responsavel = request.Responsavel.Trim();
    if (!IsSaas(auth))
    {
        var empresaUsuario = await db.Empresas.AsNoTracking().FirstOrDefaultAsync(item => item.Id == auth.EmpresaId);
        var unidadeEmpresaId = request.UnidadeEmpresaId.HasValue && empresasPermitidas.Contains(request.UnidadeEmpresaId.Value)
            ? request.UnidadeEmpresaId.Value
            : auth.EmpresaId!.Value;
        var unidadeEmpresa = await db.Empresas.AsNoTracking().FirstOrDefaultAsync(item => item.Id == unidadeEmpresaId);
        equipamento.EmpresaId = empresaUsuario?.EmpresaContratanteId ?? empresaUsuario?.Id ?? auth.EmpresaId;
        equipamento.UnidadeEmpresaId = unidadeEmpresaId;
        equipamento.Unidade = unidadeEmpresa?.NomeFantasia ?? unidadeEmpresa?.RazaoSocial ?? request.Unidade.Trim();
    }
    else
    {
        equipamento.UnidadeEmpresaId = request.UnidadeEmpresaId;
        equipamento.Unidade = request.Unidade.Trim();
    }
    equipamento.Online = request.Online;
    equipamento.UsuarioAtual = string.IsNullOrWhiteSpace(request.UsuarioAtual) ? equipamento.Responsavel : request.UsuarioAtual.Trim();
    equipamento.Filial = string.IsNullOrWhiteSpace(request.Filial) ? equipamento.Unidade : request.Filial.Trim();
    if (IsSaas(auth))
    {
        equipamento.SistemaOperacional = request.SistemaOperacional.Trim();
        equipamento.Processador = request.Processador.Trim();
        equipamento.Gpu = request.Gpu.Trim();
        equipamento.MemoriaGb = request.MemoriaGb;
        equipamento.MemoriaLivreGb = request.MemoriaLivreGb;
        equipamento.DiscoGb = request.DiscoGb;
        equipamento.DiscoLivreGb = request.DiscoLivreGb;
        equipamento.DiscosJson = request.DiscosJson;
        equipamento.Ip = request.Ip.Trim();
        equipamento.AgentId = request.AgentId.Trim();
        equipamento.AgentVersion = request.AgentVersion.Trim();
        equipamento.RustDeskId = request.RustDeskId.Trim();
        equipamento.RustDeskPassword = request.RustDeskPassword.Trim();
    }
    equipamento.UltimaLeituraEm = DateTime.UtcNow;

    if (IsSaas(auth))
    {
        if (request.EmpresaId.HasValue)
        {
            var empresa = await db.Empresas.AsNoTracking().FirstOrDefaultAsync(item => item.Id == request.EmpresaId.Value);
            equipamento.EmpresaId = empresa?.EmpresaContratanteId ?? empresa?.Id ?? request.EmpresaId;
        }
        else
        {
            equipamento.EmpresaId = request.EmpresaId;
        }
    }

    await db.SaveChangesAsync();
    return Results.Ok(equipamento);
});

app.MapDelete("/api/equipamentos/inventario/{id:int}", async (AppDbContext db, HttpContext http, int id) =>
{
    var auth = Auth(http);
    if (IsUsuario(auth))
    {
        return Results.Forbid();
    }

    var equipamento = await db.InventarioEquipamentos.FirstOrDefaultAsync(item => item.Id == id);
    if (equipamento is null)
    {
        return Results.NotFound();
    }
    if (!IsSaas(auth))
    {
        var empresasPermitidas = await EmpresasPermitidas(db, auth);
        if (!equipamento.EmpresaId.HasValue || !empresasPermitidas.Contains(equipamento.EmpresaId.Value))
        {
            return Results.Forbid();
        }
    }

    db.InventarioEquipamentos.Remove(equipamento);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapGet("/api/links", async (AppDbContext db, HttpContext http, int? empresaId) =>
{
    var auth = Auth(http);
    var query = db.LinksMonitorados.AsQueryable();

    if (IsSaas(auth) && empresaId.HasValue)
    {
        query = query.Where(item => item.EmpresaId == empresaId);
    }
    else if (!IsSaas(auth))
    {
        query = query.Where(item => item.EmpresaId == auth.EmpresaId);
    }

    var links = await query
        .OrderBy(item => item.Nome)
        .ToListAsync();

    return Results.Ok(links);
});

app.MapPost("/api/links", async (AppDbContext db, HttpContext http, LinkMonitorado link) =>
{
    var auth = Auth(http);
    if (IsUsuario(auth))
    {
        return Results.Forbid();
    }

    link.EmpresaId = IsSaas(auth) ? link.EmpresaId : auth.EmpresaId;
    link.UltimaLeituraEm = DateTime.UtcNow;
    db.LinksMonitorados.Add(link);
    await db.SaveChangesAsync();

    return Results.Created($"/api/links/{link.Id}", link);
});

app.MapPut("/api/links/{id:int}", async (AppDbContext db, HttpContext http, int id, LinkMonitorado request) =>
{
    var auth = Auth(http);
    if (IsUsuario(auth))
    {
        return Results.Forbid();
    }

    var link = await db.LinksMonitorados.FindAsync(id);
    if (link is null)
    {
        return Results.NotFound();
    }

    if (!IsSaas(auth) && link.EmpresaId != auth.EmpresaId)
    {
        return Results.Forbid();
    }

    link.EmpresaId = IsSaas(auth) ? request.EmpresaId : auth.EmpresaId;
    link.Nome = request.Nome.Trim();
    link.Tipo = request.Tipo.Trim();
    link.Local = request.Local.Trim();
    link.Firewall = request.Firewall.Trim();
    link.Endereco = request.Endereco.Trim();
    link.Cep = request.Cep.Trim();
    link.IntervaloLeituraSegundos = request.IntervaloLeituraSegundos;
    link.PingMs = request.PingMs;
    link.Latitude = request.Latitude;
    link.Longitude = request.Longitude;
    link.UltimaLeituraEm = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(link);
});

app.MapDelete("/api/links/{id:int}", async (AppDbContext db, HttpContext http, int id) =>
{
    var auth = Auth(http);
    if (IsUsuario(auth))
    {
        return Results.Forbid();
    }

    var link = await db.LinksMonitorados.FindAsync(id);
    if (link is null)
    {
        return Results.NotFound();
    }

    if (!IsSaas(auth) && link.EmpresaId != auth.EmpresaId)
    {
        return Results.Forbid();
    }

    db.LinksMonitorados.Remove(link);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapPost("/api/links/{id:int}/status", async (AppDbContext db, HttpContext http, int id, AtualizarStatusLinkRequest request) =>
{
    var auth = Auth(http);
    if (IsUsuario(auth))
    {
        return Results.Forbid();
    }

    var link = await db.LinksMonitorados.FindAsync(id);
    if (link is null)
    {
        return Results.NotFound();
    }

    if (!IsSaas(auth) && link.EmpresaId != auth.EmpresaId)
    {
        return Results.Forbid();
    }

    link.Disponivel = request.Disponivel;
    link.UltimaLeituraEm = DateTime.UtcNow;

    if (!request.Disponivel && link.ChamadoAbertoId is null)
    {
        var chamado = new Chamado
        {
            EmpresaId = link.EmpresaId,
            Numero = await GerarNumeroChamado(db),
            Solicitante = "Monitoramento de links",
            Titulo = $"Link indisponivel: {link.Nome}",
            Categoria = "Infraestrutura",
            Subcategoria = link.Tipo,
            Tipo = TipoChamado.Incidente,
            Prioridade = PrioridadeChamado.Urgente,
            Status = StatusChamado.Aberto,
            Descricao = $"Link indisponivel: {link.Nome} ({link.Endereco}). {request.Detalhes}",
            OrigemAutomacao = "Monitoramento de links"
        };

        db.Chamados.Add(chamado);
        await db.SaveChangesAsync();
        link.ChamadoAbertoId = chamado.Id;
    }

    if (request.Disponivel && link.ChamadoAbertoId is not null)
    {
        var chamado = await db.Chamados.FindAsync(link.ChamadoAbertoId.Value);
        if (chamado is not null)
        {
            chamado.Status = StatusChamado.Encerrado;
            chamado.AtualizadoEm = DateTime.UtcNow;
        }

        link.ChamadoAbertoId = null;
    }

    await db.SaveChangesAsync();
    return Results.Ok(link);
});

app.Run();

static async Task<IResult> AtualizarStatusChamado(AppDbContext db, AuthSession auth, int id, StatusChamado status)
{
    var chamado = await db.Chamados.FindAsync(id);
    if (chamado is null)
    {
        return Results.NotFound();
    }

    if (IsUsuario(auth) || !PodeAcessarChamado(auth, chamado))
    {
        return Results.Forbid();
    }

    chamado.Status = status;
    chamado.AtualizadoEm = DateTime.UtcNow;
    chamado.EncerradoEm = status is StatusChamado.Encerrado or StatusChamado.Cancelado ? DateTime.UtcNow : null;

    await db.SaveChangesAsync();
    return Results.Ok(chamado);
}

static async Task<string> GerarNumeroChamado(AppDbContext db)
{
    var totalChamados = await db.Chamados.CountAsync();

    return $"#TK-{totalChamados + 1:000}";
}

static AuthSession Auth(HttpContext http)
{
    return http.Items.TryGetValue("AuthSession", out var session) && session is AuthSession auth
        ? auth
        : throw new UnauthorizedAccessException("Sessao autenticada ausente.");
}

static bool IsSaas(AuthSession auth) => string.Equals(auth.Role, "AdministradorSaas", StringComparison.OrdinalIgnoreCase);
static bool IsCompanyAdmin(AuthSession auth) => string.Equals(auth.Role, "Administrador", StringComparison.OrdinalIgnoreCase);
static bool IsAtendente(AuthSession auth) => string.Equals(auth.Role, "Atendente", StringComparison.OrdinalIgnoreCase);
static bool IsUsuario(AuthSession auth) => string.Equals(auth.Role, "Usuario", StringComparison.OrdinalIgnoreCase);

static bool PodeAcessarChamado(AuthSession auth, Chamado chamado)
{
    if (IsSaas(auth))
    {
        return true;
    }

    if (chamado.EmpresaId != auth.EmpresaId)
    {
        return false;
    }

    if (IsUsuario(auth))
    {
        return UsuarioEhSolicitanteChamado(auth, chamado);
    }

    return true;
}

static bool UsuarioEhSolicitanteChamado(AuthSession auth, Chamado chamado)
{
    return chamado.SolicitanteUsuarioId == auth.UserId
        || string.Equals(chamado.Solicitante, auth.Nome, StringComparison.OrdinalIgnoreCase)
        || string.Equals(chamado.Solicitante, auth.Email, StringComparison.OrdinalIgnoreCase);
}

static string NormalizarStatusConhecimento(string? status)
{
    return status switch
    {
        "Publicado" => "Publicado",
        "Em revisão" => "Em revisão",
        "Rascunho" => "Rascunho",
        "Arquivado" => "Arquivado",
        _ => "Publicado"
    };
}

static async Task<List<int>> EmpresasPermitidas(AppDbContext db, AuthSession auth)
{
    if (!auth.EmpresaId.HasValue)
    {
        return [];
    }

    return await EmpresasDaContratante(db, auth.EmpresaId.Value);
}

static async Task<List<int>> EmpresasDaContratante(AppDbContext db, int empresaId)
{
    var empresaBase = await db.Empresas
        .AsNoTracking()
        .FirstOrDefaultAsync(empresa => empresa.Id == empresaId);

    if (empresaBase is null)
    {
        return [];
    }

    var contratanteId = empresaBase.EmpresaContratanteId ?? empresaBase.Id;
    return await db.Empresas
        .AsNoTracking()
        .Where(empresa => empresa.Id == contratanteId || empresa.EmpresaContratanteId == contratanteId)
        .Select(empresa => empresa.Id)
        .ToListAsync();
}

static AuthResponse CriarAuthResponse(Users usuario, AuthTokenService tokenService)
{
    return new AuthResponse
    {
        Id = usuario.Id,
        EmpresaId = usuario.EmpresaId,
        Nome = usuario.Nome,
        Email = usuario.Email,
        Role = usuario.Role,
        EmpresaNome = usuario.Empresa?.Nome,
        TenantSlug = usuario.Empresa?.TenantSlug,
        Token = tokenService.Create(usuario)
    };
}

static string? ValidarEmpresaRequest(CriarEmpresaRequest request)
{
    var camposObrigatorios = new Dictionary<string, string>
    {
        ["Razao social"] = request.RazaoSocial,
        ["Nome fantasia"] = request.NomeFantasia,
        ["CNPJ"] = request.Cnpj,
        ["Endereco"] = request.Endereco,
        ["E-mail"] = request.Email,
        ["Telefone"] = request.Telefone,
        ["CEP"] = request.Cep,
        ["Cidade"] = request.Cidade,
        ["Estado"] = request.Estado
    };

    var campoVazio = camposObrigatorios.FirstOrDefault(campo => string.IsNullOrWhiteSpace(campo.Value));
    if (!string.IsNullOrWhiteSpace(campoVazio.Key))
    {
        return $"{campoVazio.Key} e obrigatorio.";
    }

    if (!request.Email.Contains('@') || !request.Email.Contains('.'))
    {
        return "Informe um e-mail valido.";
    }

    var tipoUnidade = string.IsNullOrWhiteSpace(request.TipoUnidade)
        ? request.EmpresaContratanteId.HasValue ? "Filial" : "Contratante"
        : request.TipoUnidade.Trim();
    if (!string.IsNullOrWhiteSpace(tipoUnidade)
        && !new[] { "Contratante", "Matriz", "Filial" }.Contains(tipoUnidade, StringComparer.OrdinalIgnoreCase))
    {
        return "Tipo de empresa invalido.";
    }

    if (!string.Equals(tipoUnidade, "Contratante", StringComparison.OrdinalIgnoreCase)
        && !request.EmpresaContratanteId.HasValue)
    {
        return "Informe a empresa contratante da matriz ou filial.";
    }

    return null;
}

static string NormalizarTipoUnidade(string? tipoUnidade, int? empresaContratanteId)
{
    if (string.IsNullOrWhiteSpace(tipoUnidade))
    {
        return empresaContratanteId.HasValue ? "Filial" : "Contratante";
    }

    return tipoUnidade.Trim().Equals("Matriz", StringComparison.OrdinalIgnoreCase)
        ? "Matriz"
        : tipoUnidade.Trim().Equals("Filial", StringComparison.OrdinalIgnoreCase)
            ? "Filial"
            : "Contratante";
}

static bool EmailValido(string email)
{
    if (string.IsNullOrWhiteSpace(email))
    {
        return false;
    }

    try
    {
        var endereco = new MailAddress(email);
        return endereco.Address.Equals(email, StringComparison.OrdinalIgnoreCase)
            && email.Contains('.');
    }
    catch
    {
        return false;
    }
}

static string NormalizarTenantSlug(string value)
{
    var slug = value.Trim().ToLowerInvariant();
    return string.Concat(slug.Select(character => char.IsLetterOrDigit(character) ? character : '-')).Trim('-');
}

static string? FindAgentPackage(string contentRootPath)
{
    var candidates = new[]
    {
        Path.Combine(contentRootPath, "agent", "UniFlowIT.Agent.zip"),
        Path.Combine(contentRootPath, "UniFlowIT.Agent.zip"),
        Path.GetFullPath(Path.Combine(contentRootPath, "..", "UniFlowIT.Agent", "publish", "UniFlowIT.Agent.zip")),
        Path.GetFullPath(Path.Combine(contentRootPath, "..", "UniFlowIT.Agent", "bin", "Release", "net10.0", "win-x64", "publish", "UniFlowIT.Agent.zip"))
    };

    return candidates.FirstOrDefault(File.Exists);
}

static string? FindAgentInstaller(string contentRootPath)
{
    var candidates = new[]
    {
        Path.Combine(contentRootPath, "agent", "UniFlowIT-Agent-Setup.exe"),
        Path.Combine(contentRootPath, "UniFlowIT-Agent-Setup.exe"),
        Path.GetFullPath(Path.Combine(contentRootPath, "..", "UniFlowIT.Agent.Installer", "publish", "UniFlowIT-Agent-Setup.exe")),
        Path.GetFullPath(Path.Combine(contentRootPath, "..", "UniFlowIT.Agent.Installer", "dist", "win-x64", "UniFlowIT.Agent.Installer.exe")),
        Path.GetFullPath(Path.Combine(contentRootPath, "..", "UniFlowIT.Agent.Installer", "bin", "Release", "net10.0-windows", "win-x64", "publish", "UniFlowIT.Agent.Installer.exe"))
    };

    return candidates.FirstOrDefault(File.Exists);
}

internal sealed record TypingState(int ChamadoId, int UsuarioId, string UsuarioNome, DateTime ExpiraEm);
