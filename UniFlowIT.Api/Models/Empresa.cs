using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UniFlowIT.Api.Models
{
    public class Empresa
    {
        public int Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string RazaoSocial { get; set; } = string.Empty;
        public string NomeFantasia { get; set; } = string.Empty;
        public string TenantSlug { get; set; } = string.Empty;
        public string Cnpj { get; set; } = string.Empty;
        public string Endereco { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Numero { get; set; } = string.Empty;
        public string? Complemento { get; set; }
        public string? Bairro { get; set; }
        public string Cidade { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string Cep { get; set; } = string.Empty;
        public string? InscricaoMunicipal { get; set; }
        public string? InscricaoEstadual { get; set; }
        public string? LogoUrl { get; set; }
        public int? EmpresaContratanteId { get; set; }
        public string TipoUnidade { get; set; } = "Contratante";
        public bool Ativo { get; set; } = true;
        public bool AcessoBloqueado { get; set; } = false;
        public string? MotivoBloqueio { get; set; }
        public DateTime? BloqueadoEm { get; set; }
        public DateTime DataCadastro { get; set; } = DateTime.UtcNow;


    }
}
