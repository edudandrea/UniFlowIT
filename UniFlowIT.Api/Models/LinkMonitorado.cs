namespace UniFlowIT.Api.Models
{
    public class LinkMonitorado
    {
        public int Id { get; set; }
        public int? EmpresaId { get; set; }
        public Empresa? Empresa { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Tipo { get; set; } = "Internet";
        public string Local { get; set; } = "Matriz";
        public string Firewall { get; set; } = string.Empty;
        public string Endereco { get; set; } = string.Empty;
        public string Cep { get; set; } = string.Empty;
        public int IntervaloLeituraSegundos { get; set; } = 60;
        public int PingMs { get; set; } = 0;
        public decimal Latitude { get; set; } = -23.561m;
        public decimal Longitude { get; set; } = -46.656m;
        public bool Disponivel { get; set; } = true;
        public int? ChamadoAbertoId { get; set; }
        public DateTime UltimaLeituraEm { get; set; } = DateTime.UtcNow;
    }

    public class AtualizarStatusLinkRequest
    {
        public bool Disponivel { get; set; }
        public string Detalhes { get; set; } = string.Empty;
    }
}
