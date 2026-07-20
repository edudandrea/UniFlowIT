using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace UniFlowIT.Api.Models
{
    public class Users
    {
        public int Id { get; set; }
        public int? EmpresaId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string Login { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SenhaHash { get; set; } = string.Empty;
        public string Role { get; set; } = "Usuario";

        public bool Ativo { get; set; } = true;
        public bool MustChangePassword { get; set; }
        public int AccessFailedCount { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public string? RefreshTokenHash { get; set; }
        public DateTime? RefreshTokenExpiresAt { get; set; }
        public DateTime? RefreshTokenRevokedAt { get; set; }
        public DateTime? TemporaryPasswordExpiresAt { get; set; }
        public DateTime DataCadastro { get; set; } = DateTime.UtcNow;

        public Empresa? Empresa { get; set; }
    }
}