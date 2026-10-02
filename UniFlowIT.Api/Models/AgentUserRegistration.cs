namespace UniFlowIT.Api.Models;

public sealed class AgentUserRegistration
{
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string Ramal { get; set; } = string.Empty;
    public string Senha { get; set; } = string.Empty;
}

public sealed record AgentRemoteAccessRequest(int? EmpresaId, string RustDeskId);
