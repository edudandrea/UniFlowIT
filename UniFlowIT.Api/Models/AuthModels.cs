namespace UniFlowIT.Api.Models
{
    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;
    }

    public class CriarAdministradorSaasRequest
    {
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;
    }

    public class CriarEmpresaRequest
    {
        public string Nome { get; set; } = string.Empty;
        public string RazaoSocial { get; set; } = string.Empty;
        public string NomeFantasia { get; set; } = string.Empty;
        public string TenantSlug { get; set; } = string.Empty;
        public string Cnpj { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Endereco { get; set; } = string.Empty;
        public string Numero { get; set; } = string.Empty;
        public string? Complemento { get; set; }
        public string? Bairro { get; set; }
        public string? Cidade { get; set; }
        public string? Estado { get; set; }
        public string? Cep { get; set; }
        public string? InscricaoMunicipal { get; set; }
        public string? InscricaoEstadual { get; set; }
        public string? LogoUrl { get; set; }
        public bool Ativo { get; set; } = true;
        public bool AcessoBloqueado { get; set; }
        public string? MotivoBloqueio { get; set; }
        public DateTime? BloqueadoEm { get; set; }
    }

    public class CriarUsuarioRequest
    {
        public int EmpresaId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;
        public string Role { get; set; } = "Usuario";
    }

    public class AtualizarUsuarioRequest
    {
        public int EmpresaId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Senha { get; set; }
        public string Role { get; set; } = "Usuario";
        public bool Ativo { get; set; } = true;
    }

    public class AlterarSenhaRequest
    {
        public string SenhaAtual { get; set; } = string.Empty;
        public string NovaSenha { get; set; } = string.Empty;
    }

    public class UsuarioResponse
    {
        public int Id { get; set; }
        public int? EmpresaId { get; set; }
        public string? EmpresaNome { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool Ativo { get; set; }
    }

    public class AuthResponse
    {
        public int Id { get; set; }
        public int? EmpresaId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? EmpresaNome { get; set; }
        public string? TenantSlug { get; set; }
        public string Token { get; set; } = string.Empty;
    }
}
