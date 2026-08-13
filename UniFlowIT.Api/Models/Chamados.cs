namespace UniFlowIT.Api.Models
{
    public class Chamado
    {
        public int Id { get; set; }
        public int? EmpresaId { get; set; }
        public Empresa? Empresa { get; set; }
        public int? SolicitanteUsuarioId { get; set; }
        public string Numero { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Solicitante { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string Subcategoria { get; set; } = string.Empty;
        public TipoChamado Tipo { get; set; }
        public PrioridadeChamado Prioridade { get; set; }
        public StatusChamado Status { get; set; } = StatusChamado.Aberto;
        public string Descricao { get; set; } = string.Empty;
        public EquipamentoCapturado EquipamentoRelacionado { get; set; } = new();
        public List<AnexoChamado> Anexos { get; set; } = [];
        public List<ComunicacaoChamado> Comunicacoes { get; set; } = [];
        public int? AtendenteId { get; set; }
        public string? AtendenteNome { get; set; }
        public int? AvaliacaoNota { get; set; }
        public string? AvaliacaoComentario { get; set; }
        public string? OrigemAutomacao { get; set; }
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
        public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
        public DateTime? EncerradoEm { get; set; }
    }

    public class CriarChamadoRequest
    {
        public int? EmpresaId { get; set; }
        public int? SolicitanteUsuarioId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Solicitante { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string Subcategoria { get; set; } = string.Empty;
        public TipoChamado Tipo { get; set; }
        public PrioridadeChamado Prioridade { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public EquipamentoCapturado EquipamentoRelacionado { get; set; } = new();
        public List<AnexoChamado> Anexos { get; set; } = [];
    }

    public class AvaliarChamadoRequest
    {
        public int Nota { get; set; }
        public string Comentario { get; set; } = string.Empty;
    }

    public class EditarChamadoRequest
    {
        public string Titulo { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string Subcategoria { get; set; } = string.Empty;
        public TipoChamado Tipo { get; set; }
        public PrioridadeChamado Prioridade { get; set; }
        public StatusChamado Status { get; set; }
        public string Descricao { get; set; } = string.Empty;
    }

    public class EquipamentoCapturado
    {
        public string Hostname { get; set; } = string.Empty;
        public string SistemaOperacional { get; set; } = string.Empty;
        public string UsuarioLogado { get; set; } = string.Empty;
        public string Ip { get; set; } = string.Empty;
        public string Navegador { get; set; } = string.Empty;
    }

    public class AnexoChamado
    {
        public int Id { get; set; }
        public string NomeArquivo { get; set; } = string.Empty;
        public string TipoConteudo { get; set; } = string.Empty;
        public long TamanhoBytes { get; set; }
        public string Url { get; set; } = string.Empty;
        public DateTime EnviadoEm { get; set; } = DateTime.UtcNow;
    }

    public enum TipoChamado
    {
        Incidente,
        Solicitacao,
        Alteracao
    }

    public enum PrioridadeChamado
    {
        Baixa,
        Media,
        Alta,
        Urgente
    }

    public enum StatusChamado
    {
        Aberto,
        EmAtendimento,
        AguardandoRetorno,
        Encerrado,
        Cancelado
    }

    public class CategoriaChamado
    {
        public int Id { get; set; }
        public int? EmpresaId { get; set; }
        public Empresa? Empresa { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Subcategorias { get; set; } = string.Empty;
        public PrioridadeChamado PrioridadePadrao { get; set; } = PrioridadeChamado.Media;
        public bool Ativo { get; set; } = true;
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
        public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
    }
}
