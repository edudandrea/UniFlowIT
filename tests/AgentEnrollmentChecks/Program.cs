global using Microsoft.AspNetCore.Hosting;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using UniFlowIT.Agent;
using UniFlowIT.Api.Models;
using UniFlowIT.Api.Services;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
builder.Configuration["Security:TokenSigningKey"] = new string('x', 40);
var tokens = new AuthTokenService(builder.Configuration, builder.Environment);
var actor = new Users { Id = 12, EmpresaId = 9, Email = "admin@example.test", Role = "Administrador" };
var token = tokens.CreateEnrollment(actor, 9, 2);
Check(tokens.TryValidate(token, out var enrollment) && enrollment.Role == "AgentEnrollment" && enrollment.EmpresaId == 9, "enrollment is signed and company scoped");
Check(tokens.CreateEnrollment(actor, 9, 2) != token, "each installer has a distinct credential");
var companySession = new AuthSession(actor.Id, 9, "Admin", actor.Email, actor.Role, enrollment.ExpiresAt);
Check(AgentCompanyScope.CanDownload(companySession, 9), "company administrator can download own agent");
Check(!AgentCompanyScope.CanDownload(companySession, 10), "company administrator cannot download another company's agent");
Check(!AgentCompanyScope.CanDownload(companySession with { Role = "AdministradorSaas" }, 9), "SaaS cannot issue company installers");
Check(!AgentCompanyScope.CanDownload(companySession with { Role = "Usuario" }, 9), "common user cannot issue registration credentials");
Check(AgentCompanyScope.CanRequestRemote(companySession with { Role = "Atendente" }, 9), "attendant can request remote access within own company");
Check(!AgentCompanyScope.CanRequestRemote(companySession, 10), "remote access cannot use another company context");
Check(!AgentCompanyScope.CanRequestRemote(companySession with { Role = "Usuario" }, 9), "common user cannot request remote access");
Check(!AgentCompanyScope.CanRequestRemote(companySession with { Role = "AdministradorSaas" }, 9), "SaaS cannot request company remote access");
Check(AgentCompanyScope.MatchesEnrollment(enrollment, new Empresa { Id = 9, EmpresaContratanteId = 2 }), "credential binds both company and contracting company");
Check(!AgentCompanyScope.MatchesEnrollment(enrollment, new Empresa { Id = 10, EmpresaContratanteId = 2 }), "sibling company cannot reuse installer");
Check(!AgentCompanyScope.MatchesEnrollment(enrollment, new Empresa { Id = 9, EmpresaContratanteId = 3 }), "changed contracting company invalidates installer");
Check(!AgentCompanyScope.MatchesEnrollment(enrollment, new Empresa { Id = 9, EmpresaContratanteId = 2, AcessoBloqueado = true }), "blocked company cannot enroll");
Check(!tokens.TryValidate("x" + token, out _), "tampered credential is rejected");
var userToken = tokens.Create(new Users { Id = 15, EmpresaId = 9, Role = "Usuario", Email = "user@example.test" });
Check(tokens.TryValidate(userToken, out var session) && session.Role == "Usuario", "portal credential has common-user role");
var hash = PasswordService.Hash("User@1234");
Check(PasswordService.Verify("User@1234", hash) && !PasswordService.Verify("wrong", hash), "created password uses existing login hash verifier");

using var api = Listener();
using var agent = Listener();
using var client = new HttpClient { BaseAddress = new Uri(agent.Prefixes.Single()), Timeout = TimeSpan.FromSeconds(10) };
using var syncLock = new SemaphoreSlim(1, 1);
var config = new AgentConfig { ApiUrl = api.Prefixes.Single().TrimEnd('/'), EmpresaId = 9, EnrollmentToken = token };

async Task<HttpResponseMessage> Request(HttpRequestMessage request)
{
    var responseTask = client.SendAsync(request);
    var context = await agent.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(10));
    Check(await AgentEnrollment.HandleAsync(context, config, syncLock, CancellationToken.None), "local route handled");
    return await responseTask;
}

HttpRequestMessage Registration(string? nonce, string? origin = "http://127.0.0.1:17891")
{
    var request = new HttpRequestMessage(HttpMethod.Post, "cadastro")
    {
        Content = new StringContent(JsonSerializer.Serialize(new { nome = "Common User", email = "user@example.test", telefone = "11999999999", ramal = "123", senha = "User@1234" }), Encoding.UTF8, "application/json")
    };
    if (nonce is not null) request.Headers.Add("X-Enrollment-Nonce", nonce);
    if (origin is not null) request.Headers.Add("Origin", origin);
    return request;
}

using var page = await Request(new HttpRequestMessage(HttpMethod.Get, "cadastro"));
var html = await page.Content.ReadAsStringAsync();
var nonce = Regex.Match(html, "'X-Enrollment-Nonce':'([A-F0-9]+)'").Groups[1].Value;
Check(nonce.Length == 64 && !html.Contains(token), "form has anti-forgery nonce and never exposes enrollment credential");
using var missingNonce = await Request(Registration(null));
Check(missingNonce.StatusCode == HttpStatusCode.Forbidden, "registration without nonce is rejected");
using var wrongOrigin = await Request(Registration(nonce, "https://untrusted.example"));
Check(wrongOrigin.StatusCode == HttpStatusCode.Forbidden, "foreign-origin registration is rejected");

async Task RespondApi(int status, object body)
{
    var context = await api.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(10));
    Check(context.Request.Headers["Authorization"] == "Bearer " + token, "agent uses enrollment credential rather than admin session");
    using var payload = await JsonDocument.ParseAsync(context.Request.InputStream);
    Check(payload.RootElement.GetProperty("ramal").GetString() == "123", "contact details are forwarded");
    context.Response.StatusCode = status;
    context.Response.ContentType = "application/json";
    await context.Response.OutputStream.WriteAsync(JsonSerializer.SerializeToUtf8Bytes(body));
    context.Response.Close();
}

var rejectedApi = RespondApi(409, new { message = "E-mail ja cadastrado." });
using var rejected = await Request(Registration(nonce));
await rejectedApi;
Check(rejected.StatusCode == HttpStatusCode.Conflict && config.EnrollmentToken == token && AgentStore.Saved is null, "rejected registration preserves pending enrollment");

AgentStore.FailSave = true;
var failedSaveApi = RespondApi(200, new { id = 15, empresaId = 9, role = "Usuario", token = userToken });
var failedResponse = client.SendAsync(Registration(nonce));
var failedContext = await agent.GetContextAsync().WaitAsync(TimeSpan.FromSeconds(10));
try
{
    await AgentEnrollment.HandleAsync(failedContext, config, syncLock, CancellationToken.None);
    throw new Exception("Expected simulated save failure");
}
catch (IOException) { failedContext.Response.StatusCode = 500; failedContext.Response.Close(); }
using var saveFailure = await failedResponse;
await failedSaveApi;
Check(config.EnrollmentToken == token && config.UsuarioId is null, "disk failure preserves enrollment so registration can be retried");
AgentStore.FailSave = false;

var successApi = RespondApi(201, new { id = 15, empresaId = 9, role = "Usuario", token = userToken });
using var success = await Request(Registration(nonce));
await successApi;
Check(success.IsSuccessStatusCode && config.EnrollmentToken == "" && config.UsuarioId == 15 && config.Token == userToken, "successful enrollment switches agent to common-user identity");
Check(AgentStore.Saved is not null && !AgentStore.Saved.Contains("User@1234") && !AgentStore.Saved.Contains(token), "saved configuration excludes password and consumed enrollment credential");
using var repeated = await Request(Registration(nonce));
Check(repeated.StatusCode == HttpStatusCode.Conflict, "completed local enrollment cannot be reused");
Console.WriteLine("All enrollment checks passed.");

static void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
}

static HttpListener Listener()
{
    using var socket = new TcpListener(IPAddress.Loopback, 0);
    socket.Start();
    var port = ((IPEndPoint)socket.LocalEndpoint).Port;
    socket.Stop();
    var listener = new HttpListener();
    listener.Prefixes.Add($"http://127.0.0.1:{port}/");
    listener.Start();
    return listener;
}

internal static class AgentStore
{
    public static string? Saved;
    public static bool FailSave;
    public static void Save(AgentConfig config)
    {
        if (FailSave) throw new IOException("Simulated disk error");
        Saved = JsonSerializer.Serialize(config);
    }
}
