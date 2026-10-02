using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace UniFlowIT.Agent;

internal static class AgentEnrollment
{
    private static readonly string Nonce = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    public static async Task<bool> HandleAsync(HttpListenerContext context, AgentConfig config, SemaphoreSlim syncLock, CancellationToken cancellationToken)
    {
        if (context.Request.Url?.AbsolutePath != "/cadastro") return false;
        context.Response.Headers.Remove("Access-Control-Allow-Origin");
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; form-action 'self'; frame-ancestors 'none'";
        if (context.Request.HttpMethod == "GET")
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            var html = string.IsNullOrWhiteSpace(config.EnrollmentToken)
                ? "<!doctype html><html lang='pt-BR'><meta charset='utf-8'><title>UniFlowIT</title><p>Cadastro concluido. Use seu e-mail e senha no portal de chamados.</p></html>"
                : Page.Replace("__NONCE__", Nonce);
            await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(html), cancellationToken);
            context.Response.Close();
            return true;
        }
        if (context.Request.HttpMethod != "POST" || context.Request.Headers["X-Enrollment-Nonce"] != Nonce
            || context.Request.Headers["Origin"] != "http://127.0.0.1:17891"
            || context.Request.ContentLength64 > 16384 || context.Request.ContentLength64 < 0)
        {
            context.Response.StatusCode = 403;
            context.Response.Close();
            return true;
        }
        await syncLock.WaitAsync(cancellationToken);
        try
        {
            if (string.IsNullOrWhiteSpace(config.EnrollmentToken))
            {
                await Reply(context, 409, "Cadastro ja concluido.");
                return true;
            }
            using var payload = await JsonDocument.ParseAsync(context.Request.InputStream, cancellationToken: cancellationToken);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.EnrollmentToken);
            using var response = await http.PostAsJsonAsync($"{config.ApiUrl.TrimEnd('/')}/agent/cadastro-usuario", payload.RootElement, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? "Instalador expirado ou empresa indisponivel. Solicite um novo instalador ao administrador."
                    : "Nao foi possivel cadastrar. Confira os dados e tente novamente.";
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                try { using var error = JsonDocument.Parse(body); message = error.RootElement.GetProperty("message").GetString() ?? message; }
                catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException) { }
                await Reply(context, (int)response.StatusCode, message);
                return true;
            }
            var user = await response.Content.ReadFromJsonAsync<RegisteredUser>(cancellationToken);
            if (user is null || user.Role != "Usuario" || user.EmpresaId != config.EmpresaId || string.IsNullOrWhiteSpace(user.Token))
                throw new InvalidOperationException("Resposta de cadastro invalida.");
            var enrollmentToken = config.EnrollmentToken;
            var previousToken = config.Token;
            var previousUser = config.UsuarioId;
            config.Token = user.Token;
            config.UsuarioId = user.Id;
            config.EnrollmentToken = string.Empty;
            try { AgentStore.Save(config); }
            catch
            {
                config.EnrollmentToken = enrollmentToken;
                config.Token = previousToken;
                config.UsuarioId = previousUser;
                throw;
            }
            await Reply(context, 200, "Cadastro concluido! Acesse o portal de chamados com seu e-mail e a senha criada.");
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException)
        { await Reply(context, 503, "Servidor indisponivel. Tente novamente; seus dados ainda nao foram confirmados."); }
        finally { syncLock.Release(); }
        return true;
    }

    private static async Task Reply(HttpListenerContext context, int status, string message)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.OutputStream.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(new { message }));
        context.Response.Close();
    }

    private sealed record RegisteredUser(int Id, int? EmpresaId, string Role, string Token);

    private const string Page = """
    <!doctype html>
    <html lang="pt-BR"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Cadastro — UniFlowIT Agent</title>
    <style>
    *{box-sizing:border-box}body{margin:0;background:#081220;color:#eef2ff;font:16px 'Segoe UI',sans-serif;display:grid;min-height:100vh;place-items:center;padding:24px}
    main{width:min(100%,480px);background:#1c273a;padding:32px;border-radius:16px}h1{margin-top:0}p{line-height:1.5;color:#cbd5e1}label{display:block;margin-top:16px}input{display:block;width:100%;padding:12px;margin-top:6px;border:1px solid #64748b;border-radius:6px;font:inherit}button{width:100%;padding:14px;margin-top:24px;background:#2563eb;color:white;border:0;border-radius:6px;font:inherit;cursor:pointer}button:disabled{opacity:.6}#message{white-space:pre-wrap}
    </style></head><body><main><h1>Seu acesso aos chamados</h1><p>O agente foi instalado. Complete seu cadastro para acessar o portal. Sua empresa sera vinculada automaticamente.</p>
    <form id="registration">
    <label>Nome completo<input name="nome" autocomplete="name" maxlength="160" required></label>
    <label>E-mail (seu login)<input name="email" type="email" autocomplete="email" maxlength="80" required></label>
    <label>Telefone de contato<input name="telefone" type="tel" autocomplete="tel" maxlength="30" required></label>
    <label>Ramal<input name="ramal" maxlength="20" required></label>
    <label>Senha<input name="senha" type="password" autocomplete="new-password" minlength="8" required></label>
    <p>Use pelo menos 8 caracteres, letra maiuscula, numero e caractere especial.</p>
    <button id="submit" type="submit">Criar meu acesso</button></form><p id="message" role="status" aria-live="polite"></p>
    <script>
    const form=document.querySelector('#registration'),button=document.querySelector('#submit'),message=document.querySelector('#message');
    form.addEventListener('submit',async event=>{event.preventDefault();button.disabled=true;message.textContent='Cadastrando...';
    try{const response=await fetch('/cadastro',{method:'POST',headers:{'Content-Type':'application/json','X-Enrollment-Nonce':'__NONCE__'},body:JSON.stringify(Object.fromEntries(new FormData(form)))});const result=await response.json();message.textContent=result.message;if(response.ok){form.reset();form.hidden=true;}}
    catch{message.textContent='Nao foi possivel concluir. Verifique sua conexao e tente novamente.';}finally{button.disabled=false;}});
    </script></main></body></html>
    """;
}
