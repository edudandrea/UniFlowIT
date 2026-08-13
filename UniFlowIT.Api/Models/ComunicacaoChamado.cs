namespace UniFlowIT.Api.Models
{
    public class ComunicacaoChamado
    {
        public int Id { get; set; }
        public int ChamadoId { get; set; }
        public int AutorId { get; set; }
        public string AutorNome { get; set; } = string.Empty;
        public string AutorPerfil { get; set; } = "Usuario";
        public string Mensagem { get; set; } = string.Empty;
        public string Tipo { get; set; } = "Mural";
        public DateTime EnviadoEm { get; set; } = DateTime.UtcNow;
        public bool Lida { get; set; }
    }

    public class CriarMensagemRequest
    {
        public int AutorId { get; set; }
        public string AutorNome { get; set; } = string.Empty;
        public string AutorPerfil { get; set; } = "Usuario";
        public string Mensagem { get; set; } = string.Empty;
        public string Tipo { get; set; } = "Mural";
    }
}
