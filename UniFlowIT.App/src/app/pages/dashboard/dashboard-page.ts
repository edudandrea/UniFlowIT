import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { Chamado, Inventario, LinkMonitorado, Pagina, UsuarioLista } from '../../models/uniflowit.models';

interface DashboardCard {
  label: string;
  value: string | number;
  tone: 'blue' | 'red' | 'amber' | 'green' | 'cyan' | 'purple' | 'gray';
}

interface PieSlice {
  label: string;
  value: number;
  color: string;
  path: string;
  percent: number;
  critical?: boolean;
}

@Component({
  selector: 'app-dashboard-page',
  imports: [CommonModule],
  templateUrl: './dashboard-page.html',
  styleUrls: ['./dashboard-page.scss'],
})
export class DashboardPage {
  @Input() empresaNome = 'Empresa nao identificada';
  @Input() unidade = 'Todas';
  @Input() periodo = 'Ultimos 30 dias';
  @Input() chamados: Chamado[] = [];
  @Input() inventario: Inventario[] = [];
  @Input() links: LinkMonitorado[] = [];
  @Input() usuarios: UsuarioLista[] = [];

  @Output() navegarClick = new EventEmitter<Pagina>();

  protected get chamadosAbertos(): Chamado[] {
    return this.chamados.filter((chamado) => chamado.status === 'Aberto' || chamado.status === 'Em atendimento');
  }

  protected get chamadosResolvidos(): Chamado[] {
    return this.chamados.filter((chamado) => chamado.status === 'Encerrado');
  }

  protected get chamadosCriticos(): Chamado[] {
    return this.chamados.filter((chamado) => chamado.prioridade === 'Urgente' || chamado.prioridade === 'Alta');
  }

  protected get linksFora(): LinkMonitorado[] {
    return this.links.filter((link) => !link.disponivel);
  }

  protected get satisfacaoMedia(): string {
    const notas = this.chamados.map((chamado) => chamado.avaliacao).filter((nota): nota is number => typeof nota === 'number');
    if (!notas.length) {
      return '-';
    }

    return (notas.reduce((total, nota) => total + nota, 0) / notas.length).toFixed(1);
  }

  protected get cardsPrimarios(): DashboardCard[] {
    return [
      { label: 'Tickets abertos', value: this.chamadosAbertos.length, tone: 'blue' },
      { label: 'Tickets criticos', value: this.chamadosCriticos.length, tone: 'red' },
      { label: 'SLA vencido', value: this.chamados.filter((chamado) => chamado.prioridade === 'Urgente' && chamado.status !== 'Encerrado').length, tone: 'amber' },
      { label: 'Ativos cadastrados', value: this.inventario.length, tone: 'cyan' },
      { label: 'Equipamentos offline', value: this.linksFora.length, tone: 'red' },
      { label: 'Alertas de infraestrutura', value: this.linksFora.length + this.chamados.filter((chamado) => chamado.categoria === 'Infraestrutura' && chamado.status !== 'Encerrado').length, tone: 'purple' },
    ];
  }

  protected get cardsSecundarios(): DashboardCard[] {
    const totalChamados = this.chamados.length;
    const resolvidos = this.chamadosResolvidos.length;
    const slaCumprido = totalChamados ? Math.round((resolvidos / totalChamados) * 100) : 0;

    return [
      { label: 'Resolvidos no periodo', value: resolvidos, tone: 'green' },
      { label: 'SLA cumprido', value: `${slaCumprido}%`, tone: 'green' },
      { label: 'Tempo medio de resolucao', value: resolvidos ? 'Calculando' : '-', tone: 'cyan' },
      { label: 'Satisfacao dos usuarios', value: this.satisfacaoMedia, tone: 'purple' },
      { label: 'Solicitacoes pendentes', value: this.chamados.filter((chamado) => chamado.tipo === 'Solicitacao' && chamado.status !== 'Encerrado').length, tone: 'amber' },
      { label: 'Licencas vencendo', value: 0, tone: 'gray' },
    ];
  }

  protected chamadosPorStatus(): Array<{ label: string; value: number }> {
    const status = ['Aberto', 'Em atendimento', 'Encerrado', 'Cancelado'];
    return status.map((label) => ({
      label,
      value: this.chamados.filter((chamado) => chamado.status === label).length,
    }));
  }

  protected chamadosPorPrioridade(): Array<{ label: string; value: number; critical: boolean }> {
    const prioridades = ['Urgente', 'Alta', 'Media', 'Baixa'];
    return prioridades.map((label) => ({
      label,
      value: this.chamados.filter((chamado) => chamado.prioridade === label).length,
      critical: label === 'Urgente' || label === 'Alta',
    }));
  }

  protected graficoStatus(): PieSlice[] {
    return this.criarFatias(
      this.chamadosPorStatus().map((item, index) => ({
        ...item,
        color: ['#38bdf8', '#8b5cf6', '#10b981', '#64748b'][index],
      })),
    );
  }

  protected graficoPrioridade(): PieSlice[] {
    return this.criarFatias(
      this.chamadosPorPrioridade().map((item, index) => ({
        ...item,
        color: ['#ef4444', '#f59e0b', '#38bdf8', '#10b981'][index],
      })),
    );
  }

  protected totalGraficoStatus(): number {
    return this.chamadosPorStatus().reduce((total, item) => total + item.value, 0);
  }

  protected totalGraficoPrioridade(): number {
    return this.chamadosPorPrioridade().reduce((total, item) => total + item.value, 0);
  }

  protected graficoSla(): PieSlice[] {
    return this.criarFatias(
      this.indicadoresSla().map((item, index) => ({
        label: item.label,
        value: this.valorIndicadorSla(item.value),
        color: ['#2563eb', '#10b981', '#ef4444', '#8b5cf6', '#f59e0b'][index],
      })),
    );
  }

  protected totalGraficoSla(): number {
    return this.indicadoresSla().reduce((total, item) => total + this.valorIndicadorSla(item.value), 0);
  }

  protected corStatus(label: string): string {
    return ({
      Aberto: '#38bdf8',
      'Em atendimento': '#8b5cf6',
      Encerrado: '#10b981',
      Cancelado: '#64748b',
    } as Record<string, string>)[label] ?? '#38bdf8';
  }

  protected corPrioridade(label: string): string {
    return ({
      Urgente: '#ef4444',
      Alta: '#f59e0b',
      Media: '#38bdf8',
      Baixa: '#10b981',
    } as Record<string, string>)[label] ?? '#38bdf8';
  }

  protected corSla(label: string): string {
    return ({
      'Primeira resposta': '#2563eb',
      Resolucao: '#10b981',
      'Criticos em aberto': '#ef4444',
      'Resolucao primeiro contato': '#8b5cf6',
      'Taxa de reabertura': '#f59e0b',
    } as Record<string, string>)[label] ?? '#38bdf8';
  }

  protected chamadosAtencao(): Chamado[] {
    return [...this.chamados]
      .filter((chamado) => chamado.status !== 'Encerrado' && chamado.status !== 'Cancelado')
      .sort((a, b) => this.pesoAtencao(b) - this.pesoAtencao(a))
      .slice(0, 6);
  }

  protected indicadoresSla(): Array<{ label: string; value: string }> {
    const total = this.chamados.length;
    const resolvidos = this.chamadosResolvidos.length;
    const criticosAbertos = this.chamadosCriticos.filter((chamado) => chamado.status !== 'Encerrado').length;
    const reabertos = this.chamados.filter((chamado) => chamado.mensagens.some((mensagem) => mensagem.texto.toLowerCase().includes('reabert'))).length;

    return [
      { label: 'Primeira resposta', value: total ? `${Math.round(((total - this.chamados.filter((chamado) => !chamado.mensagens.length).length) / total) * 100)}%` : '-' },
      { label: 'Resolucao', value: total ? `${Math.round((resolvidos / total) * 100)}%` : '-' },
      { label: 'Criticos em aberto', value: String(criticosAbertos) },
      { label: 'Resolucao primeiro contato', value: total ? `${Math.round((this.chamados.filter((chamado) => chamado.status === 'Encerrado' && chamado.mensagens.length <= 2).length / total) * 100)}%` : '-' },
      { label: 'Taxa de reabertura', value: total ? `${((reabertos / total) * 100).toFixed(1)}%` : '-' },
    ];
  }

  protected saudeInfraestrutura(): Array<{ servico: string; situacao: string; disponibilidade: string; evento: string; down: boolean }> {
    return this.links.map((link) => ({
      servico: link.nome,
      situacao: link.disponivel ? 'Online' : 'Indisponivel',
      disponibilidade: link.disponivel ? '100%' : '0%',
      evento: link.chamado ? `Ticket ${link.chamado}` : 'Sem incidentes ativos',
      down: !link.disponivel,
    }));
  }

  protected ativosAtencao(): Array<{ ativo: string; usuario: string; problema: string; risco: string }> {
    const ativosSemUsuario = this.inventario
      .filter((ativo) => !ativo.usuario || ativo.usuario === 'Sistema')
      .map((ativo) => ({
        ativo: ativo.hostname || ativo.patrimonio,
        usuario: ativo.usuario || 'Sem responsavel',
        problema: ativo.usuario ? 'Ativo tecnico sem usuario final' : 'Sem responsavel',
        risco: ativo.usuario ? 'Medio' : 'Alto',
      }));

    const linksOffline = this.linksFora.map((link) => ({
      ativo: link.firewall || link.nome,
      usuario: 'Infraestrutura',
      problema: `Link indisponivel: ${link.endereco}`,
      risco: 'Critico',
    }));

    return [...linksOffline, ...ativosSemUsuario].slice(0, 5);
  }

  protected desempenhoEquipe(): Array<{ tecnico: string; ativos: number; resolvidos: number; sla: string; avaliacao: string }> {
    const atendentes = this.usuarios.filter((usuario) => usuario.role === 'Atendente' || usuario.role === 'Administrador');
    return atendentes.map((tecnico) => {
      const chamadosTecnico = this.chamados.filter((chamado) => chamado.atendente === tecnico.nome);
      const resolvidos = chamadosTecnico.filter((chamado) => chamado.status === 'Encerrado').length;
      const notas = chamadosTecnico.map((chamado) => chamado.avaliacao).filter((nota): nota is number => typeof nota === 'number');
      return {
        tecnico: tecnico.nome,
        ativos: chamadosTecnico.filter((chamado) => chamado.status !== 'Encerrado' && chamado.status !== 'Cancelado').length,
        resolvidos,
        sla: chamadosTecnico.length ? `${Math.round((resolvidos / chamadosTecnico.length) * 100)}%` : '-',
        avaliacao: notas.length ? (notas.reduce((total, nota) => total + nota, 0) / notas.length).toFixed(1) : '-',
      };
    });
  }

  protected problemasRecorrentes(): Array<{ label: string; value: number }> {
    const mapa = new Map<string, number>();
    this.chamados.forEach((chamado) => {
      const chave = chamado.subcategoria || chamado.categoria;
      mapa.set(chave, (mapa.get(chave) ?? 0) + 1);
    });

    return [...mapa.entries()]
      .sort((a, b) => b[1] - a[1])
      .slice(0, 5)
      .map(([label, value]) => ({ label, value }));
  }

  private pesoAtencao(chamado: Chamado): number {
    const prioridade = chamado.prioridade === 'Urgente' ? 5 : chamado.prioridade === 'Alta' ? 4 : chamado.prioridade === 'Media' ? 2 : 1;
    const semTecnico = chamado.atendente ? 0 : 2;
    const aberto = chamado.status === 'Aberto' ? 1 : 0;
    return prioridade + semTecnico + aberto;
  }

  private criarFatias(items: Array<{ label: string; value: number; color: string; critical?: boolean }>): PieSlice[] {
    const total = items.reduce((soma, item) => soma + item.value, 0);
    if (!total) {
      return [];
    }

    let anguloAtual = -90;
    return items
      .filter((item) => item.value > 0)
      .map((item) => {
        const angulo = (item.value / total) * 360;
        const inicio = anguloAtual;
        const fim = anguloAtual + Math.min(angulo, 359.99);
        anguloAtual += angulo;

        return {
          ...item,
          percent: Math.round((item.value / total) * 100),
          path: this.descreverFatiaPizza(50, 50, 42, inicio, fim),
        };
      });
  }

  private descreverFatiaPizza(cx: number, cy: number, raio: number, inicio: number, fim: number): string {
    const inicioExterno = this.polarParaCartesiano(cx, cy, raio, fim);
    const fimExterno = this.polarParaCartesiano(cx, cy, raio, inicio);
    const arcoGrande = fim - inicio <= 180 ? '0' : '1';

    return [
      `M ${cx} ${cy}`,
      `L ${inicioExterno.x} ${inicioExterno.y}`,
      `A ${raio} ${raio} 0 ${arcoGrande} 0 ${fimExterno.x} ${fimExterno.y}`,
      'Z',
    ].join(' ');
  }

  private valorIndicadorSla(valor: string): number {
    const numero = Number.parseFloat(valor.replace('%', '').replace(',', '.'));
    return Number.isFinite(numero) ? numero : 0;
  }

  private polarParaCartesiano(cx: number, cy: number, raio: number, angulo: number): { x: string; y: string } {
    const radianos = (angulo * Math.PI) / 180;
    return {
      x: (cx + raio * Math.cos(radianos)).toFixed(3),
      y: (cy + raio * Math.sin(radianos)).toFixed(3),
    };
  }
}
