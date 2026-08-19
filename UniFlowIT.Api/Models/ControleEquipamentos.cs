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
        public string Marca { get; set; } = string.Empty;
        public string Modelo { get; set; } = string.Empty;
        public string Tipo { get; set; } = "Computador";
        public string Descricao { get; set; } = string.Empty;
        public DateTime? DataCompra { get; set; }
        public string NumeroNotaFiscal { get; set; } = string.Empty;
        public string NotaFiscalNome { get; set; } = string.Empty;
        public string NotaFiscalUrl { get; set; } = string.Empty;
        public int? ResponsavelUsuarioId { get; set; }
        public string Responsavel { get; set; } = string.Empty;
        public int? UnidadeEmpresaId { get; set; }
        public string Unidade { get; set; } = string.Empty;
        public bool Online { get; set; } = true;
        public string UsuarioAtual { get; set; } = string.Empty;
        public string Filial { get; set; } = string.Empty;
        public string SistemaOperacional { get; set; } = string.Empty;
        public string Processador { get; set; } = string.Empty;
        public string Gpu { get; set; } = string.Empty;
        public int MemoriaGb { get; set; }
        public int MemoriaLivreGb { get; set; }
        public int DiscoGb { get; set; }
        public int DiscoLivreGb { get; set; }
        public string DiscosJson { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public string AgentId { get; set; } = string.Empty;
        public string AgentVersion { get; set; } = string.Empty;
        public string RustDeskId { get; set; } = string.Empty;
        public string RustDeskPassword { get; set; } = string.Empty;
        public DateTime UltimaLeituraEm { get; set; } = DateTime.UtcNow;
    }

    public class AgentEquipmentRequest
    {
        public int? EquipamentoId { get; set; }
        public int? EmpresaId { get; set; }
        public string AgentId { get; set; } = string.Empty;
        public string AgentVersion { get; set; } = string.Empty;
        public string Patrimonio { get; set; } = string.Empty;
        public string Hostname { get; set; } = string.Empty;
        public string Processador { get; set; } = string.Empty;
        public string Gpu { get; set; } = string.Empty;
        public int MemoriaGb { get; set; }
        public int MemoriaLivreGb { get; set; }
        public int DiscoGb { get; set; }
        public int DiscoLivreGb { get; set; }
        public List<AgentDiskRequest> Discos { get; set; } = new();
        public string Ip { get; set; } = string.Empty;
        public string SistemaOperacional { get; set; } = string.Empty;
        public string RustDeskId { get; set; } = string.Empty;
        public string RustDeskPassword { get; set; } = string.Empty;
    }

    public class AgentEquipmentSyncResponse
    {
        public bool Created { get; set; }
        public InventarioEquipamento Equipamento { get; set; } = new();
    }

    public class AgentDiskRequest
    {
        public string Nome { get; set; } = string.Empty;
        public string Unidade { get; set; } = string.Empty;
        public int TotalGb { get; set; }
        public int LivreGb { get; set; }
    }

    public enum StatusEnvioEquipamento
    {
        Preparando,
        Enviado,
        Recebido,
        Cancelado
    }
}
