namespace UniFlowIT.Api.Models
{
    public class ArtigoConhecimento
    {
        public int Id { get; set; }
        public int? EmpresaId { get; set; }
        public Empresa? Empresa { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string Conteudo { get; set; } = string.Empty;
        public string[] Tags { get; set; } = [];
        public bool Publicado { get; set; } = true;
        public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
    }
}
