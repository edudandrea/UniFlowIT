namespace UniFlowIT.Api.Models
{
    public class ControleEquipamento
    {
        public int Id { get; set; }
        public int? EmpresaId { get; set; }
        public Empresa? Empresa { get; set; }
        public string Patrimonio { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public string Fabricante { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public string Serial { get; set; } = string.Empty;
        public string FilialOrigem { get; set; } = string.Empty;
        public string FilialDestino { get; set; } = string.Empty;
        public string ResponsavelEnvio { get; set; } = string.Empty;
        public string ResponsavelRecebimento { get; set; } = string.Empty;
        public StatusEnvioEquipamento Status { get; set; } = StatusEnvioEquipamento.Preparando;
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
        public DateTime? EnviadoEm { get; set; }
        public DateTime? RecebidoEm { get; set; }
    }

    public class InventarioEquipamento
    {
        public int Id { get; set; }
        public int? EmpresaId { get; set; }
        public Empresa? Empresa { get; set; }
        public string Patrimonio { get; set; } = string.Empty;
        public string Hostname { get; set; } = string.Empty;
        public string UsuarioAtual { get; set; } = string.Empty;
        public string Filial { get; set; } = string.Empty;
        public string SistemaOperacional { get; set; } = string.Empty;
        public string Processador { get; set; } = string.Empty;
        public int MemoriaGb { get; set; }
        public int DiscoGb { get; set; }
        public string Ip { get; set; } = string.Empty;
        public DateTime UltimaLeituraEm { get; set; } = DateTime.UtcNow;
    }

    public enum StatusEnvioEquipamento
    {
        Preparando,
        Enviado,
        Recebido,
        Cancelado
    }
}
