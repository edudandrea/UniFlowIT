using UniFlowIT.Api.Models;

namespace UniFlowIT.Api.Services;

public static class AgentCompanyScope
{
    public static bool CanDownload(AuthSession session, int empresaId) =>
        session.Role == "Administrador" && session.EmpresaId == empresaId && empresaId > 0;

    public static bool CanRequestRemote(AuthSession session, int? empresaId) =>
        session.Role is "Administrador" or "Atendente"
        && session.EmpresaId.HasValue && session.EmpresaId == empresaId;

    public static bool MatchesEnrollment(AuthSession enrollment, Empresa empresa) =>
        enrollment.Role == "AgentEnrollment"
        && enrollment.EmpresaId == empresa.Id
        && enrollment.EmpresaContratanteId == (empresa.EmpresaContratanteId ?? empresa.Id)
        && empresa.Ativo && !empresa.AcessoBloqueado;
}
