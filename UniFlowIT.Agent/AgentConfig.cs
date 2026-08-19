namespace UniFlowIT.Agent;

internal sealed class AgentConfig
{
    public string AgentId { get; set; } = Guid.NewGuid().ToString("N");
    public int? EquipamentoId { get; set; }
    public string ApiUrl { get; set; } = "http://localhost:5151/api";
    public string Token { get; set; } = string.Empty;
    public int? EmpresaId { get; set; }
    public int? UsuarioId { get; set; }
    public string RustDeskPassword { get; set; } = string.Empty;
    public DateTime? UltimaTentativaRustDeskEm { get; set; }
}
