using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Text.Json.Serialization;
using UniFlowIT.Api.Data;
using UniFlowIT.Api.Models;
using UniFlowIT.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseHttpsRedirection();

app.MapGet("/", () => Results.Ok(new { name = "UniFlowIT.Api", phase = "Fase 1 - Central de chamados" }));

app.MapGet("/api/auth/bootstrap-status", async (AppDbContext db) =>
{
    var existeAdministradorSaas = await db.Users.AnyAsync(user => user.Role.ToLower() == "administradorsaas");
    return Results.Ok(new { existeAdministradorSaas });
});

app.MapPost("/api/auth/criar-administrador-saas", async (AppDbContext db, CriarAdministradorSaasRequest request) =>
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

    return Results.Created($"/api/usuarios/{usuario.Id}", CriarAuthResponse(usuario));
});

app.MapPost("/api/auth/login", async (AppDbContext db, LoginRequest request) =>
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

    return Results.Ok(CriarAuthResponse(usuario));
});

app.MapGet("/api/empresas", async (AppDbContext db) =>
{
    var empresas = await db.Empresas
        .OrderBy(empresa => empresa.Nome)
        .ToListAsync();

    return Results.Ok(empresas);
});

app.MapPost("/api/empresas", async (AppDbContext db, CriarEmpresaRequest request) =>
{
    var erroValidacao = ValidarEmpresaRequest(request);
    if (erroValidacao is not null)
    {
        return Results.BadRequest(new { message = erroValidacao });
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

app.MapPut("/api/empresas/{id:int}", async (AppDbContext db, int id, CriarEmpresaRequest request) =>
{
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
    empresa.Ativo = request.Ativo;
    empresa.AcessoBloqueado = request.AcessoBloqueado;
    empresa.MotivoBloqueio = request.MotivoBloqueio;
    empresa.BloqueadoEm = request.BloqueadoEm;

    await db.SaveChangesAsync();
    return Results.Ok(empresa);
});

app.MapGet("/api/usuarios", async (AppDbContext db, int? empresaId) =>
{
    var query = db.Users
        .Include(user => user.Empresa)
        .AsQueryable();

    if (empresaId.HasValue)
    {
        query = query.Where(user => user.EmpresaId == empresaId);
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

app.MapPost("/api/usuarios", async (AppDbContext db, CriarUsuarioRequest request) =>
{
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

app.MapPut("/api/usuarios/{id:int}", async (AppDbContext db, int id, AtualizarUsuarioRequest request) =>
{
    var usuario = await db.Users
        .Include(user => user.Empresa)
        .FirstOrDefaultAsync(user => user.Id == id);

    if (usuario is null)
    {
        return Results.NotFound();
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

app.MapPut("/api/usuarios/{id:int}/senha", async (AppDbContext db, int id, AlterarSenhaRequest request) =>
{
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

app.MapGet("/api/chamados", async (AppDbContext db, string? perfil, int? empresaId, int? usuarioId, int? atendenteId, string? solicitante) =>
{
    var query = db.Chamados
        .Include(chamado => chamado.Anexos)
        .Include(chamado => chamado.Comunicacoes)
        .AsQueryable();

    if (!string.Equals(perfil, "AdministradorSaas", StringComparison.OrdinalIgnoreCase) && empresaId.HasValue)
    {
        query = query.Where(chamado => chamado.EmpresaId == empresaId);
    }

    if (string.Equals(perfil, "Atendente", StringComparison.OrdinalIgnoreCase))
    {
        query = query.Where(chamado => chamado.Status == StatusChamado.Aberto || chamado.AtendenteId == atendenteId);
    }
    else if (string.Equals(perfil, "Usuario", StringComparison.OrdinalIgnoreCase))
    {
        query = usuarioId.HasValue
            ? query.Where(chamado => chamado.SolicitanteUsuarioId == usuarioId)
            : query.Where(chamado => chamado.Solicitante == solicitante);
        query = query.Where(chamado => chamado.Status == StatusChamado.Aberto);
    }

    var chamados = await query
        .OrderByDescending(chamado => chamado.CriadoEm)
        .ToListAsync();

    return Results.Ok(chamados);
});

app.MapGet("/api/chamados/{id:int}", async (AppDbContext db, int id) =>
{
    var chamado = await db.Chamados
        .Include(item => item.Anexos)
        .Include(item => item.Comunicacoes.OrderBy(mensagem => mensagem.EnviadoEm))
        .FirstOrDefaultAsync(item => item.Id == id);

    return chamado is null ? Results.NotFound() : Results.Ok(chamado);
});

app.MapGet("/api/categorias-chamado", async (AppDbContext db, int? empresaId) =>
{
    var query = db.CategoriasChamados.AsQueryable();

    if (empresaId.HasValue)
    {
        query = query.Where(categoria => categoria.EmpresaId == empresaId);
    }

    var categorias = await query
        .OrderBy(categoria => categoria.Nome)
        .ToListAsync();

    return Results.Ok(categorias);
});

app.MapPost("/api/categorias-chamado", async (AppDbContext db, CategoriaChamado categoria) =>
{
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

app.MapPut("/api/categorias-chamado/{id:int}", async (AppDbContext db, int id, CategoriaChamado request) =>
{
    var categoria = await db.CategoriasChamados.FindAsync(id);
    if (categoria is null)
    {
        return Results.NotFound();
    }

    categoria.Nome = request.Nome.Trim();
    categoria.Subcategorias = request.Subcategorias.Trim();
    categoria.PrioridadePadrao = request.PrioridadePadrao;
    categoria.Ativo = request.Ativo;
    categoria.AtualizadoEm = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(categoria);
});

app.MapPost("/api/chamados", async (AppDbContext db, CriarChamadoRequest request) =>
{
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

app.MapPost("/api/chamados/{id:int}/capturar", async (AppDbContext db, int id, Users atendente) =>
{
    var chamado = await db.Chamados.FindAsync(id);
    if (chamado is null)
    {
        return Results.NotFound();
    }

    chamado.AtendenteId = atendente.Id;
    chamado.AtendenteNome = atendente.Nome;
    chamado.Status = StatusChamado.EmAtendimento;
    chamado.AtualizadoEm = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(chamado);
});

app.MapPost("/api/chamados/{id:int}/mensagens", async (AppDbContext db, int id, CriarMensagemRequest request) =>
{
    var chamadoExiste = await db.Chamados.AnyAsync(item => item.Id == id);
    if (!chamadoExiste)
    {
        return Results.NotFound();
    }

    var mensagem = new ComunicacaoChamado
    {
        ChamadoId = id,
        AutorId = request.AutorId,
        AutorNome = request.AutorNome,
        AutorPerfil = request.AutorPerfil,
        Mensagem = request.Mensagem
    };

    db.ComunicacoesChamados.Add(mensagem);
    await db.Chamados
        .Where(item => item.Id == id)
        .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.AtualizadoEm, DateTime.UtcNow));
    await db.SaveChangesAsync();

    return Results.Created($"/api/chamados/{id}/mensagens/{mensagem.Id}", mensagem);
});

app.MapPost("/api/chamados/{id:int}/encerrar", async (AppDbContext db, int id) => await AtualizarStatusChamado(db, id, StatusChamado.Encerrado));
app.MapPost("/api/chamados/{id:int}/cancelar", async (AppDbContext db, int id) => await AtualizarStatusChamado(db, id, StatusChamado.Cancelado));

app.MapPost("/api/chamados/{id:int}/avaliar", async (AppDbContext db, int id, AvaliarChamadoRequest request) =>
{
    var chamado = await db.Chamados.FindAsync(id);
    if (chamado is null)
    {
        return Results.NotFound();
    }

    chamado.AvaliacaoNota = Math.Clamp(request.Nota, 1, 5);
    chamado.AvaliacaoComentario = request.Comentario;
    chamado.AtualizadoEm = DateTime.UtcNow;

    await db.SaveChangesAsync();
    return Results.Ok(chamado);
});

app.MapGet("/api/base-conhecimento", async (AppDbContext db, int? empresaId) =>
{
    var query = db.BaseConhecimento
        .Where(item => item.Publicado);

    if (empresaId.HasValue)
    {
        query = query.Where(item => item.EmpresaId == empresaId || item.EmpresaId == null);
    }

    var artigos = await query
        .OrderBy(item => item.Categoria)
        .ThenBy(item => item.Titulo)
        .ToListAsync();

    return Results.Ok(artigos);
});

app.MapPost("/api/base-conhecimento", async (AppDbContext db, ArtigoConhecimento artigo) =>
{
    artigo.AtualizadoEm = DateTime.UtcNow;
    db.BaseConhecimento.Add(artigo);
    await db.SaveChangesAsync();

    return Results.Created($"/api/base-conhecimento/{artigo.Id}", artigo);
});

app.MapGet("/api/equipamentos/envios", async (AppDbContext db, int? empresaId) =>
{
    var query = db.ControleEquipamentos.AsQueryable();

    if (empresaId.HasValue)
    {
        query = query.Where(item => item.EmpresaId == empresaId);
    }

    var envios = await query
        .OrderByDescending(item => item.CriadoEm)
        .ToListAsync();

    return Results.Ok(envios);
});

app.MapPost("/api/equipamentos/envios", async (AppDbContext db, ControleEquipamento envio) =>
{
    envio.CriadoEm = DateTime.UtcNow;
    db.ControleEquipamentos.Add(envio);
    await db.SaveChangesAsync();

    return Results.Created($"/api/equipamentos/envios/{envio.Id}", envio);
});

app.MapGet("/api/equipamentos/inventario", async (AppDbContext db, int? empresaId) =>
{
    var query = db.InventarioEquipamentos.AsQueryable();

    if (empresaId.HasValue)
    {
        query = query.Where(item => item.EmpresaId == empresaId);
    }

    var inventario = await query
        .OrderBy(item => item.Filial)
        .ThenBy(item => item.Hostname)
        .ToListAsync();

    return Results.Ok(inventario);
});

app.MapPost("/api/equipamentos/inventario", async (AppDbContext db, InventarioEquipamento equipamento) =>
{
    equipamento.UltimaLeituraEm = DateTime.UtcNow;
    db.InventarioEquipamentos.Add(equipamento);
    await db.SaveChangesAsync();

    return Results.Created($"/api/equipamentos/inventario/{equipamento.Id}", equipamento);
});

app.MapGet("/api/links", async (AppDbContext db, int? empresaId) =>
{
    var query = db.LinksMonitorados.AsQueryable();

    if (empresaId.HasValue)
    {
        query = query.Where(item => item.EmpresaId == empresaId);
    }

    var links = await query
        .OrderBy(item => item.Nome)
        .ToListAsync();

    return Results.Ok(links);
});

app.MapPost("/api/links", async (AppDbContext db, LinkMonitorado link) =>
{
    db.LinksMonitorados.Add(link);
    await db.SaveChangesAsync();

    return Results.Created($"/api/links/{link.Id}", link);
});

app.MapPost("/api/links/{id:int}/status", async (AppDbContext db, int id, AtualizarStatusLinkRequest request) =>
{
    var link = await db.LinksMonitorados.FindAsync(id);
    if (link is null)
    {
        return Results.NotFound();
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

    await db.SaveChangesAsync();
    return Results.Ok(link);
});

app.Run();

static async Task<IResult> AtualizarStatusChamado(AppDbContext db, int id, StatusChamado status)
{
    var chamado = await db.Chamados.FindAsync(id);
    if (chamado is null)
    {
        return Results.NotFound();
    }

    chamado.Status = status;
    chamado.AtualizadoEm = DateTime.UtcNow;
    chamado.EncerradoEm = status is StatusChamado.Encerrado or StatusChamado.Cancelado ? DateTime.UtcNow : null;

    await db.SaveChangesAsync();
    return Results.Ok(chamado);
}

static async Task<string> GerarNumeroChamado(AppDbContext db)
{
    var hoje = DateTime.UtcNow.Date;
    var amanha = hoje.AddDays(1);
    var quantidadeHoje = await db.Chamados.CountAsync(item => item.CriadoEm >= hoje && item.CriadoEm < amanha);

    return $"CH-{DateTime.UtcNow:yyyyMMdd}-{quantidadeHoje + 1:0000}";
}

static AuthResponse CriarAuthResponse(Users usuario)
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
        Token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
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

    return null;
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
