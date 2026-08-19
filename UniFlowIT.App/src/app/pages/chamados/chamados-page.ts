import { CommonModule } from '@angular/common';
import { AfterViewChecked, Component, ElementRef, EventEmitter, HostListener, Input, OnChanges, OnDestroy, Output, SimpleChanges, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CategoriaChamado, Chamado, Mensagem, Perfil, Prioridade } from '../../models/uniflowit.models';

export type ModoVisualizacaoTickets = 'lista' | 'kanban' | 'compacta';
export type FiltroStatusTickets = 'Todos' | 'Abertos' | 'Em atendimento' | 'Aguardando' | 'Resolvidos' | 'Fechados';
type AbaDetalheChamado = 'dados' | 'anexos' | 'avaliacao' | 'comunicacao' | 'chat' | 'linha-do-tempo';

interface EventoLinhaDoTempo {
  titulo: string;
  detalhe: string;
  autor: string;
  horario: string;
  tipo: 'abertura' | 'status' | 'mensagem' | 'anexo' | 'avaliacao' | 'atualizacao';
}

@Component({
  selector: 'app-chamados-page',
  imports: [CommonModule, FormsModule],
  templateUrl: './chamados-page.html',
  styleUrls: ['./chamados-page.scss'],
})
export class ChamadosPage implements AfterViewChecked, OnChanges, OnDestroy {
  @Input({ required: true }) perfil!: Perfil;
  @Input() metricas = { abertos: 0, atendimento: 0, urgentes: 0, linksFora: 0 };
  @Input() modoDesenvolvedor = false;
  @Input() categorias: CategoriaChamado[] = [];
  @Input() chamadosVisiveis: Chamado[] = [];
  @Input() chamadoSelecionado?: Chamado;
  @Input() usuarioAtualId?: number;
  @Input() usuarioAtualNome = '';
  @Input() usuariosDigitandoChat: string[] = [];
  @Input() abrirChamadoModal?: { id: number; aba: AbaDetalheChamado; nonce: number } | null;
  @Input() chamadosComAtualizacao: Set<number> = new Set<number>();
  @Input({ required: true }) novoChamado!: {
    titulo: string;
    solicitante: string;
    departamento?: string;
    equipamento?: string;
    categoria: string;
    subcategoria: string;
    tipo: Chamado['tipo'];
    prioridade: Prioridade;
    descricao: string;
    anexos: string;
    anexosDetalhes: Array<{ nomeArquivo: string; tipoConteudo: string; tamanhoBytes: number; url: string }>;
  };

  // Listas auxiliares do modal de novo chamado
  protected readonly departamentosLista = [
    'Tecnologia da Informação',
    'Comercial',
    'Financeiro',
    'Recursos Humanos',
    'Operações',
    'Administrativo',
    'Diretoria',
  ];

  protected readonly equipamentosLista = [
    'NOTE-EDUARDO',
    'DESKTOP-FIN01',
    'NOTE-TI03',
    'DESKTOP-ADM02',
    'Nenhum',
  ];

  protected arrastandoAnexoNovoChamado = false;

  // Visualização e Filtros principais
  protected modoVisualizacao: ModoVisualizacaoTickets = 'lista';
  protected filtroStatus: FiltroStatusTickets = 'Todos';
  protected filtroPrioridade = 'Todas';
  protected filtroCategoria = 'Todas';
  protected filtroTecnico = 'Todos';
  protected filtroTickets = '';

  // Paginação e Seleção
  protected paginaAtual = 1;
  protected itensPorPagina = 5;
  protected readonly opcoesItensPorPagina = [5, 10, 20, 50];
  protected menuAcoesAbertoId: number | null = null;
  protected menuFiltrosAberto = false;

  // Kanban Configurações
  protected ordenarKanbanPor: 'prioridade' | 'recente' | 'sla' = 'prioridade';
  protected agruparKanbanPor: 'status' | 'prioridade' | 'categoria' = 'status';
  protected kanbanFullscreen = false;

  // Modais e Chat
  protected novaMensagem = '';
  protected novoComunicado = '';
  protected digitandoRemoto = false;
  protected avaliacaoSelecionada = 5;
  protected avaliacaoComentario = '';
  protected chamadoModalAberto = false;
  protected novoChamadoModalAberto = false;
  protected abaDetalhe: AbaDetalheChamado = 'dados';
  protected chamadoEdicao = this.criarEdicaoVazia();
  protected chamadoArrastadoId: number | null = null;
  protected paginaComunicados = 0;
  private readonly comunicadosPorPagina = 3;
  private readonly prioridades: Prioridade[] = ['Baixa', 'Media', 'Alta', 'Urgente'];

  protected readonly colunasKanban: Array<{
    id: string;
    titulo: string;
    status: Chamado['status'];
    cor: string;
    dotClass: string;
  }> = [
    { id: 'aberto', titulo: 'Aberto', status: 'Aberto', cor: '#10b981', dotClass: 'open' },
    { id: 'atendimento', titulo: 'Em atendimento', status: 'Em atendimento', cor: '#f59e0b', dotClass: 'in-progress' },
    { id: 'aguardando', titulo: 'Aguardando', status: 'Aguardando retorno', cor: '#eab308', dotClass: 'waiting' },
    { id: 'resolvido', titulo: 'Resolvido', status: 'Encerrado', cor: '#059669', dotClass: 'resolved' },
    { id: 'fechado', titulo: 'Fechado', status: 'Cancelado', cor: '#64748b', dotClass: 'closed' },
  ];

  private readonly typingStorageKey = 'uniflowit-ticket-typing';
  private ocultarDigitandoTimer?: number;
  private ultimoChatScrollKey = '';
  private forcarScrollChat = false;

  @ViewChild('chatMessages') private chatMessages?: ElementRef<HTMLElement>;

  @Output() abrirChamadoSubmit = new EventEmitter<void>();
  @Output() selecionarChamadoClick = new EventEmitter<number>();
  @Output() salvarChamadoClick = new EventEmitter<Partial<Chamado> & { id: number }>();
  @Output() capturarChamadoClick = new EventEmitter<Chamado>();
  @Output() encerrarChamadoClick = new EventEmitter<Chamado>();
  @Output() cancelarChamadoClick = new EventEmitter<Chamado>();
  @Output() enviarMensagemClick = new EventEmitter<string>();
  @Output() enviarComunicacaoClick = new EventEmitter<string>();
  @Output() digitandoChat = new EventEmitter<number>();
  @Output() anexarArquivosClick = new EventEmitter<{ chamado: Chamado; arquivos: File[] }>();
  @Output() excluirAnexoClick = new EventEmitter<{ chamado: Chamado; anexoId?: number; nome: string }>();
  @Output() movimentoInvalidoClick = new EventEmitter<void>();
  @Output() avaliarChamadoClick = new EventEmitter<{ chamado: Chamado; avaliacao: number; comentario: string }>();
  @Output() detalheModalAbertoChange = new EventEmitter<boolean>();
  @Output() acessoRemotoClick = new EventEmitter<Chamado>();

  constructor() {
    window.addEventListener('storage', this.processarDigitacaoRemota);
  }

  @HostListener('document:click')
  protected fecharMenusAoClicarFora(): void {
    this.menuAcoesAbertoId = null;
    this.menuFiltrosAberto = false;
  }

  ngOnDestroy(): void {
    window.removeEventListener('storage', this.processarDigitacaoRemota);
    window.clearTimeout(this.ocultarDigitandoTimer);
    this.detalheModalAbertoChange.emit(false);
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['abrirChamadoModal'] || !this.abrirChamadoModal) {
      return;
    }

    const chamado = this.chamadosVisiveis.find((item) => item.id === this.abrirChamadoModal?.id);
    if (!chamado) {
      return;
    }

    this.abrirDetalheChamado(chamado, this.abrirChamadoModal.aba);
  }

  ngAfterViewChecked(): void {
    if (this.abaDetalhe !== 'chat') {
      return;
    }

    const chaveAtual = this.chaveScrollChat();
    if (this.forcarScrollChat || chaveAtual !== this.ultimoChatScrollKey) {
      this.ultimoChatScrollKey = chaveAtual;
      this.forcarScrollChat = false;
      this.rolarChatParaRodape();
    }
  }

  // Alternância de abas de visualização
  protected alterarModoVisualizacao(modo: ModoVisualizacaoTickets): void {
    this.modoVisualizacao = modo;
  }

  protected definirFiltroStatus(status: FiltroStatusTickets): void {
    this.filtroStatus = status;
    this.paginaAtual = 1;
  }

  protected alternarMenuFiltros(event: Event): void {
    event.stopPropagation();
    this.menuFiltrosAberto = !this.menuFiltrosAberto;
  }

  protected alternarMenuAcoes(id: number, event: Event): void {
    event.stopPropagation();
    this.menuAcoesAbertoId = this.menuAcoesAbertoId === id ? null : id;
  }

  protected alternarKanbanFullscreen(): void {
    this.kanbanFullscreen = !this.kanbanFullscreen;
  }

  // Filtragem dos chamados
  protected chamadosFiltrados(): Chamado[] {
    let lista = this.chamadosVisiveis;

    // Busca textual rápida
    const termo = this.filtroTickets.trim().toLocaleLowerCase('pt-BR');
    if (termo) {
      lista = lista.filter((chamado) => {
        const texto = [
          chamado.numero,
          chamado.titulo,
          chamado.descricao,
          chamado.categoria,
          chamado.subcategoria,
          chamado.prioridade,
          chamado.atendente,
          chamado.solicitante,
          chamado.equipamento,
        ]
          .filter(Boolean)
          .join(' ')
          .toLocaleLowerCase('pt-BR');

        return texto.includes(termo);
      });
    }

    // Filtro por pílula de status
    if (this.filtroStatus === 'Abertos') {
      lista = lista.filter((c) => c.status === 'Aberto');
    } else if (this.filtroStatus === 'Em atendimento') {
      lista = lista.filter((c) => c.status === 'Em atendimento');
    } else if (this.filtroStatus === 'Aguardando') {
      lista = lista.filter((c) => c.status === 'Aguardando retorno');
    } else if (this.filtroStatus === 'Resolvidos') {
      lista = lista.filter((c) => c.status === 'Encerrado');
    } else if (this.filtroStatus === 'Fechados') {
      lista = lista.filter((c) => c.status === 'Cancelado' || c.status === 'Encerrado');
    }

    // Filtro por Prioridade
    if (this.filtroPrioridade !== 'Todas') {
      lista = lista.filter((c) => c.prioridade === this.filtroPrioridade);
    }

    // Filtro por Categoria
    if (this.filtroCategoria !== 'Todas') {
      lista = lista.filter((c) => c.categoria === this.filtroCategoria);
    }

    // Filtro por Técnico
    if (this.filtroTecnico === 'NaoAtribuido') {
      lista = lista.filter((c) => !c.atendente);
    } else if (this.filtroTecnico !== 'Todos') {
      lista = lista.filter((c) => c.atendente === this.filtroTecnico);
    }

    return lista;
  }

  // Paginação
  protected chamadosPaginados(): Chamado[] {
    const filtrados = this.chamadosFiltrados();
    const inicio = (this.paginaAtual - 1) * this.itensPorPagina;
    return filtrados.slice(inicio, inicio + this.itensPorPagina);
  }

  protected totalChamadosFiltrados(): number {
    return this.chamadosFiltrados().length;
  }

  protected totalPaginas(): number {
    return Math.max(1, Math.ceil(this.totalChamadosFiltrados() / this.itensPorPagina));
  }

  protected irParaPagina(pagina: number): void {
    if (pagina >= 1 && pagina <= this.totalPaginas()) {
      this.paginaAtual = pagina;
    }
  }

  protected alterarItensPorPagina(quantidade: number): void {
    this.itensPorPagina = quantidade;
    this.paginaAtual = 1;
  }

  protected paginasExibidas(): Array<number | string> {
    const total = this.totalPaginas();
    const atual = this.paginaAtual;

    if (total <= 7) {
      return Array.from({ length: total }, (_, i) => i + 1);
    }

    if (atual <= 4) {
      return [1, 2, 3, 4, 5, '...', total];
    }

    if (atual >= total - 3) {
      return [1, '...', total - 4, total - 3, total - 2, total - 1, total];
    }

    return [1, '...', atual - 1, atual, atual + 1, '...', total];
  }

  protected indiceInicioPaginacao(): number {
    if (this.totalChamadosFiltrados() === 0) return 0;
    return (this.paginaAtual - 1) * this.itensPorPagina + 1;
  }

  protected indiceFimPaginacao(): number {
    return Math.min(this.paginaAtual * this.itensPorPagina, this.totalChamadosFiltrados());
  }

  // Contadores para as pílulas de status
  protected totalPorStatus(statusFiltro: FiltroStatusTickets): number {
    if (statusFiltro === 'Todos') return this.chamadosVisiveis.length;
    if (statusFiltro === 'Abertos') return this.chamadosVisiveis.filter((c) => c.status === 'Aberto').length;
    if (statusFiltro === 'Em atendimento') return this.chamadosVisiveis.filter((c) => c.status === 'Em atendimento').length;
    if (statusFiltro === 'Aguardando') return this.chamadosVisiveis.filter((c) => c.status === 'Aguardando retorno').length;
    if (statusFiltro === 'Resolvidos') return this.chamadosVisiveis.filter((c) => c.status === 'Encerrado').length;
    if (statusFiltro === 'Fechados') return this.chamadosVisiveis.filter((c) => c.status === 'Cancelado' || c.status === 'Encerrado').length;
    return 0;
  }

  // Helpers de Apresentação visual
  protected obterSLA(chamado: Chamado): { tempo: string; venceEm: string; status: 'normal' | 'alerta' | 'vencido' | 'concluido' } {
    if (chamado.status === 'Encerrado' || chamado.status === 'Cancelado') {
      return { tempo: '—', venceEm: '', status: 'concluido' };
    }

    const agora = Date.now();
    const baseTimestamp = chamado.criadoEm ? new Date(chamado.criadoEm).getTime() : agora - chamado.id * 1800000;

    const duracaoMinutosPorPrioridade: Record<Prioridade, number> = {
      Urgente: 60,
      Alta: 120,
      Media: 240,
      Baixa: 480,
    };

    const duracaoMinutos = duracaoMinutosPorPrioridade[chamado.prioridade] ?? 240;
    const prazoFinal = baseTimestamp + duracaoMinutos * 60 * 1000;
    const diffMinutos = Math.round((prazoFinal - agora) / 60000);

    const prazoData = new Date(prazoFinal);
    const horaVencimento = !isNaN(prazoData.getTime())
      ? prazoData.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' })
      : '18:00';

    if (diffMinutos <= 0) {
      const atrasoMin = Math.abs(diffMinutos);
      const textoAtraso = atrasoMin > 60 ? `${Math.floor(atrasoMin / 60)}h ${atrasoMin % 60}m` : `${atrasoMin} min`;
      return { tempo: `Vencido (${textoAtraso})`, venceEm: `Venceu ${horaVencimento}`, status: 'vencido' };
    }

    const horas = Math.floor(diffMinutos / 60);
    const minutos = diffMinutos % 60;
    const tempoTexto = horas > 0 ? `${horas}h ${minutos > 0 ? minutos + 'm' : ''}` : `${minutos} min`;
    const statusSla = diffMinutos < 40 ? 'alerta' : 'normal';

    return { tempo: tempoTexto, venceEm: `Vence ${horaVencimento}`, status: statusSla };
  }

  protected formatarAbertura(chamado: Chamado): { hora: string; data: string } {
    if (chamado.criadoEm) {
      const d = new Date(chamado.criadoEm);
      if (!isNaN(d.getTime())) {
        return {
          hora: d.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' }),
          data: d.toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: 'numeric' }),
        };
      }
    }
    return { hora: '10:40', data: '20/05/2024' };
  }

  protected obterIniciais(nome?: string): string {
    if (!nome || !nome.trim()) return '—';
    const partes = nome.trim().split(/\s+/);
    if (partes.length === 1) return partes[0].substring(0, 2).toUpperCase();
    return (partes[0][0] + partes[partes.length - 1][0]).toUpperCase();
  }

  protected obterCorAvatar(nome?: string): string {
    if (!nome || !nome.trim()) return '#64748b';
    const cores = ['#2563eb', '#0d9488', '#d97706', '#7c3aed', '#db2777', '#0891b2', '#4f46e5', '#ea580c'];
    let hash = 0;
    for (let i = 0; i < nome.length; i++) {
      hash = nome.charCodeAt(i) + ((hash << 5) - hash);
    }
    const index = Math.abs(hash) % cores.length;
    return cores[index];
  }

  protected listaTecnicos(): string[] {
    const tecnicos = new Set<string>();
    for (const c of this.chamadosVisiveis) {
      if (c.atendente?.trim()) {
        tecnicos.add(c.atendente.trim());
      }
    }
    return Array.from(tecnicos);
  }

  protected limparFiltrosTickets(): void {
    this.filtroTickets = '';
    this.filtroStatus = 'Todos';
    this.filtroPrioridade = 'Todas';
    this.filtroCategoria = 'Todas';
    this.filtroTecnico = 'Todos';
    this.paginaAtual = 1;
  }

  // Kanban Métodos
  protected chamadosPorStatusKanban(coluna: (typeof this.colunasKanban)[number]): Chamado[] {
    let chamados = this.chamadosFiltrados();

    if (coluna.id === 'aberto') {
      chamados = chamados.filter((c) => c.status === 'Aberto');
    } else if (coluna.id === 'atendimento') {
      chamados = chamados.filter((c) => c.status === 'Em atendimento');
    } else if (coluna.id === 'aguardando') {
      chamados = chamados.filter((c) => c.status === 'Aguardando retorno');
    } else if (coluna.id === 'resolvido') {
      chamados = chamados.filter((c) => c.status === 'Encerrado');
    } else if (coluna.id === 'fechado') {
      chamados = chamados.filter((c) => c.status === 'Cancelado');
    }

    if (this.ordenarKanbanPor === 'prioridade') {
      const peso: Record<Prioridade, number> = { Urgente: 4, Alta: 3, Media: 2, Baixa: 1 };
      chamados = chamados.slice().sort((a, b) => (peso[b.prioridade] ?? 0) - (peso[a.prioridade] ?? 0));
    } else if (this.ordenarKanbanPor === 'recente') {
      chamados = chamados.slice().sort((a, b) => b.id - a.id);
    }

    return chamados;
  }

  protected iniciarArraste(chamado: Chamado): void {
    if (!this.podeEditarChamado() || chamado.status === 'Encerrado' || chamado.status === 'Cancelado') {
      return;
    }

    this.chamadoArrastadoId = chamado.id;
  }

  protected permitirSoltar(event: DragEvent): void {
    event.preventDefault();
  }

  protected soltarChamado(statusDestino: Chamado['status']): void {
    const chamado = this.chamadosVisiveis.find((item) => item.id === this.chamadoArrastadoId);
    this.chamadoArrastadoId = null;

    if (!chamado || chamado.status === statusDestino) {
      return;
    }

    if (!this.podeEditarChamado() || !this.movimentoPermitido(chamado.status, statusDestino)) {
      this.movimentoInvalidoClick.emit();
      return;
    }

    this.salvarChamadoClick.emit({ id: chamado.id, status: statusDestino });
  }

  protected statusVisual(status: Chamado['status']): string {
    if (status === 'Aberto') return 'Aberto';
    if (status === 'Em atendimento') return 'Em atendimento';
    if (status === 'Aguardando retorno') return 'Aguardando';
    if (status === 'Encerrado') return 'Resolvido';
    if (status === 'Cancelado') return 'Fechado';
    return status;
  }

  protected chamadoTemAtualizacao(chamado: Chamado): boolean {
    return this.chamadosComAtualizacao.has(chamado.id);
  }

  // Modais de Criação e Detalhes
  protected abrirModalNovoChamado(): void {
    this.sincronizarCategoriaSelecionada();
    this.novoChamadoModalAberto = true;
  }

  protected fecharModalNovoChamado(): void {
    this.novoChamadoModalAberto = false;
  }

  protected salvarNovoChamado(): void {
    this.abrirChamadoSubmit.emit();
    this.novoChamadoModalAberto = false;
  }

  protected abrirDetalheChamado(chamado: Chamado, aba: AbaDetalheChamado = 'dados'): void {
    this.selecionarChamadoClick.emit(chamado.id);
    this.prepararEdicao(chamado);
    this.prepararAvaliacao(chamado);
    this.paginaComunicados = 0;
    this.abaDetalhe = aba;
    this.chamadoModalAberto = true;
    this.detalheModalAbertoChange.emit(true);
    if (aba === 'chat') {
      this.agendarScrollChat();
    }
  }

  protected fecharDetalheChamado(): void {
    this.chamadoModalAberto = false;
    this.novaMensagem = '';
    this.novoComunicado = '';
    this.digitandoRemoto = false;
    this.detalheModalAbertoChange.emit(false);
  }

  protected definirAbaDetalhe(aba: AbaDetalheChamado): void {
    this.abaDetalhe = aba;
    if (aba === 'comunicacao') {
      this.paginaComunicados = 0;
    }
    if (aba === 'chat') {
      this.agendarScrollChat();
    }
  }

  protected categoriaSelecionada(): CategoriaChamado | undefined {
    return this.categorias.find((categoria) => categoria.nome === this.novoChamado.categoria);
  }

  protected subcategoriasCategoriaSelecionada(): string[] {
    return this.categoriaSelecionada()?.subcategorias ?? [];
  }

  protected definirCategoria(nome: string): void {
    const categoria = this.categorias.find((item) => item.nome === nome);
    this.novoChamado.categoria = categoria?.nome ?? nome;
    this.novoChamado.subcategoria = categoria?.subcategorias[0] ?? '';
    this.novoChamado.prioridade = categoria?.prioridadePadrao ?? this.novoChamado.prioridade;
  }

  protected definirCategoriaEdicao(nome: string): void {
    const categoria = this.categorias.find((item) => item.nome === nome);
    this.chamadoEdicao.categoria = categoria?.nome ?? nome;
    this.chamadoEdicao.subcategoria = categoria?.subcategorias[0] ?? this.chamadoEdicao.subcategoria;
  }

  protected subcategoriasEdicao(): string[] {
    return this.categorias.find((categoria) => categoria.nome === this.chamadoEdicao.categoria)?.subcategorias ?? [];
  }

  protected podeEditarChamado(): boolean {
    return this.perfil === 'Atendente' || this.perfil === 'Administrador' || this.perfil === 'AdministradorSaas';
  }

  protected podeEditarConteudoChamado(): boolean {
    if (!this.chamadoSelecionado || this.chamadoSelecionado.status === 'Encerrado' || this.chamadoSelecionado.status === 'Cancelado') {
      return false;
    }

    return (
      this.podeEditarChamado() ||
      this.chamadoSelecionado.solicitanteUsuarioId === this.usuarioAtualId ||
      this.chamadoSelecionado.solicitante === this.usuarioAtualNome
    );
  }

  protected podeReabrirChamado(): boolean {
    if (!this.chamadoSelecionado || this.chamadoSelecionado.status !== 'Encerrado' || this.perfil !== 'Usuario') {
      return false;
    }

    return this.chamadoSelecionado.solicitanteUsuarioId === this.usuarioAtualId || this.chamadoSelecionado.solicitante === this.usuarioAtualNome;
  }

  protected chamadoEncerrado(): boolean {
    return this.chamadoSelecionado?.status === 'Encerrado';
  }

  protected podeEditarAvaliacaoChamado(): boolean {
    if (!this.chamadoSelecionado || this.chamadoSelecionado.status !== 'Encerrado') {
      return false;
    }

    return this.chamadoSelecionado.solicitanteUsuarioId === this.usuarioAtualId || this.chamadoSelecionado.solicitante === this.usuarioAtualNome;
  }

  protected definirAvaliacao(nota: number): void {
    if (!this.podeEditarAvaliacaoChamado()) {
      return;
    }

    this.avaliacaoSelecionada = nota;
  }

  protected descricaoAvaliacao(nota = this.avaliacaoSelecionada): string {
    const descricoes: Record<number, string> = {
      1: 'Pessimo',
      2: 'Ruim',
      3: 'Bom',
      4: 'Satisfatorio',
      5: 'Excelente',
    };

    return descricoes[nota] ?? 'Sem avaliacao';
  }

  protected salvarAvaliacaoChamado(): void {
    if (!this.chamadoSelecionado || !this.podeEditarAvaliacaoChamado()) {
      return;
    }

    this.avaliarChamadoClick.emit({
      chamado: this.chamadoSelecionado,
      avaliacao: this.avaliacaoSelecionada,
      comentario: this.avaliacaoComentario.trim(),
    });
  }

  protected comunicacoesMural(): Mensagem[] {
    return (this.chamadoSelecionado?.mensagens.filter((mensagem) => mensagem.tipo === 'Mural') ?? [])
      .slice()
      .sort((a, b) => (b.timestamp ?? 0) - (a.timestamp ?? 0) || (b.id ?? 0) - (a.id ?? 0));
  }

  protected comunicacoesMuralPaginadas(): Mensagem[] {
    const inicio = this.paginaComunicados * this.comunicadosPorPagina;
    return this.comunicacoesMural().slice(inicio, inicio + this.comunicadosPorPagina);
  }

  protected totalPaginasComunicados(): number {
    return Math.max(1, Math.ceil(this.comunicacoesMural().length / this.comunicadosPorPagina));
  }

  protected alterarPaginaComunicados(delta: number): void {
    const proximaPagina = this.paginaComunicados + delta;
    this.paginaComunicados = Math.min(Math.max(proximaPagina, 0), this.totalPaginasComunicados() - 1);
  }

  protected mensagensChat(): Mensagem[] {
    return (this.chamadoSelecionado?.mensagens.filter((mensagem) => mensagem.tipo !== 'Mural') ?? [])
      .slice()
      .sort((a, b) => (a.timestamp ?? 0) - (b.timestamp ?? 0) || (a.id ?? 0) - (b.id ?? 0));
  }

  protected trackMensagem(index: number, mensagem: Mensagem): string {
    return `${mensagem.id ?? index}-${mensagem.enviadoEm ?? mensagem.horario}-${mensagem.autor}`;
  }

  protected mensagemEhMinha(mensagem: Mensagem): boolean {
    const usuario = this.usuarioAtualNome.trim();
    return usuario ? mensagem.autor === usuario : mensagem.perfil === this.perfil;
  }

  protected statusMensagem(mensagem: Mensagem): 'Enviando' | 'Recebida' | 'Lida' {
    if ((mensagem.id ?? 0) < 0) {
      return 'Enviando';
    }

    const mensagens = this.mensagensChat();
    const indice = mensagens.indexOf(mensagem);
    const teveRespostaDepois = mensagens.slice(indice + 1).some((item) => !this.mensagemEhMinha(item));

    return teveRespostaDepois ? 'Lida' : 'Recebida';
  }

  protected autorDigitando(): string {
    if (this.usuariosDigitandoChat.length) {
      return this.usuariosDigitandoChat.join(', ');
    }

    if (this.perfil === 'Usuario') {
      return this.chamadoSelecionado?.atendente || 'Atendimento';
    }

    return this.chamadoSelecionado?.solicitante || 'Usuario';
  }

  protected tamanhoAnexo(bytes?: number): string {
    if (!bytes || bytes <= 0) {
      return 'Tamanho nao informado';
    }

    if (bytes < 1024) {
      return `${bytes} B`;
    }

    if (bytes < 1024 * 1024) {
      return `${(bytes / 1024).toFixed(1)} KB`;
    }

    return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  }

  protected salvarEdicaoChamado(): void {
    if (!this.chamadoSelecionado || !this.podeEditarConteudoChamado()) {
      return;
    }

    const dadosBasicos = {
      id: this.chamadoSelecionado.id,
      titulo: this.chamadoEdicao.titulo.trim(),
      descricao: this.chamadoEdicao.descricao.trim(),
    };

    if (!this.podeEditarChamado()) {
      this.salvarChamadoClick.emit(dadosBasicos);
      this.fecharDetalheChamado();
      return;
    }

    this.salvarChamadoClick.emit({
      ...dadosBasicos,
      categoria: this.chamadoEdicao.categoria,
      subcategoria: this.chamadoEdicao.subcategoria,
      tipo: this.chamadoEdicao.tipo,
      prioridade: this.chamadoEdicao.prioridade,
      status: this.chamadoEdicao.status,
    });
    this.fecharDetalheChamado();
  }

  protected reabrirChamado(): void {
    if (!this.chamadoSelecionado || !this.podeReabrirChamado()) {
      return;
    }

    this.salvarChamadoClick.emit({ id: this.chamadoSelecionado.id, status: 'Aberto' });
    this.fecharDetalheChamado();
  }

  protected eventosLinhaDoTempo(): EventoLinhaDoTempo[] {
    const chamado = this.chamadoSelecionado;
    if (!chamado) {
      return [];
    }

    const eventos: EventoLinhaDoTempo[] = [
      {
        titulo: 'Chamado aberto',
        detalhe: chamado.titulo || 'Solicitação registrada no sistema',
        autor: chamado.solicitante,
        horario: this.formatarDataEvento(chamado.criadoEm),
        tipo: 'abertura',
      },
    ];

    if (chamado.atendente) {
      eventos.push({
        titulo: 'Atendimento atribuído',
        detalhe: `Responsável: ${chamado.atendente}`,
        autor: chamado.atendente,
        horario: this.formatarDataEvento(chamado.atualizadoEm),
        tipo: 'atualizacao',
      });
    }

    if (chamado.status !== 'Aberto') {
      eventos.push({
        titulo: `Status alterado para ${this.statusVisual(chamado.status)}`,
        detalhe: 'O andamento do chamado foi atualizado',
        autor: chamado.atendente || 'Sistema',
        horario: this.formatarDataEvento(chamado.atualizadoEm),
        tipo: 'status',
      });
    }

    for (const mensagem of chamado.mensagens ?? []) {
      eventos.push({
        titulo: mensagem.tipo === 'Mural' ? 'Comunicação publicada' : 'Mensagem enviada',
        detalhe: mensagem.texto,
        autor: mensagem.autor,
        horario: mensagem.horario || this.formatarDataEvento(mensagem.enviadoEm),
        tipo: 'mensagem',
      });
    }

    for (const anexo of chamado.anexosDetalhes ?? []) {
      eventos.push({
        titulo: 'Anexo adicionado',
        detalhe: anexo.nome,
        autor: chamado.solicitante,
        horario: this.formatarDataEvento(chamado.atualizadoEm),
        tipo: 'anexo',
      });
    }

    if (chamado.avaliacao) {
      eventos.push({
        titulo: 'Atendimento avaliado',
        detalhe: `${chamado.avaliacao}/5${chamado.avaliacaoComentario ? ` - ${chamado.avaliacaoComentario}` : ''}`,
        autor: chamado.solicitante,
        horario: this.formatarDataEvento(chamado.atualizadoEm),
        tipo: 'avaliacao',
      });
    }

    return eventos.sort((a, b) => this.valorDataEvento(a.horario) - this.valorDataEvento(b.horario));
  }

  private formatarDataEvento(valor?: string): string {
    if (!valor) {
      return 'Data não informada';
    }

    const data = new Date(valor);
    return Number.isNaN(data.getTime()) ? valor : data.toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' });
  }

  private valorDataEvento(valor: string): number {
    const data = new Date(valor);
    return Number.isNaN(data.getTime()) ? 0 : data.getTime();
  }

  protected alternarPrioridade(): void {
    const indiceAtual = this.prioridades.indexOf(this.novoChamado.prioridade);
    this.novoChamado.prioridade = this.prioridades[(indiceAtual + 1) % this.prioridades.length];
  }

  protected nomesAnexos(): string[] {
    return (this.novoChamado.anexosDetalhes || []).map((item) => item.nomeArquivo);
  }

  protected async selecionarAnexos(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const arquivos = Array.from(input.files ?? []);
    if (!arquivos.length) return;
    await this.processarArquivosNovoChamado(arquivos);
    input.value = '';
  }

  protected onDragOverNovoChamado(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.arrastandoAnexoNovoChamado = true;
  }

  protected onDragLeaveNovoChamado(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.arrastandoAnexoNovoChamado = false;
  }

  protected async onDropNovoChamado(event: DragEvent): Promise<void> {
    event.preventDefault();
    event.stopPropagation();
    this.arrastandoAnexoNovoChamado = false;
    const arquivos = Array.from(event.dataTransfer?.files ?? []);
    if (arquivos.length) {
      await this.processarArquivosNovoChamado(arquivos);
    }
  }

  private async processarArquivosNovoChamado(arquivos: File[]): Promise<void> {
    const novosDetalhes = await Promise.all(
      arquivos.map(async (arquivo) => ({
        nomeArquivo: arquivo.name,
        tipoConteudo: arquivo.type,
        tamanhoBytes: arquivo.size,
        url: await this.arquivoParaDataUrl(arquivo),
      }))
    );

    const existentes = this.novoChamado.anexosDetalhes || [];
    const mesclados = [...existentes, ...novosDetalhes];
    this.novoChamado.anexosDetalhes = mesclados;
    this.novoChamado.anexos = mesclados.map((item) => item.nomeArquivo).join(', ');
  }

  protected removerAnexoNovoChamado(index: number): void {
    const detalhes = (this.novoChamado.anexosDetalhes || []).slice();
    detalhes.splice(index, 1);
    this.novoChamado.anexosDetalhes = detalhes;
    this.novoChamado.anexos = detalhes.map((item) => item.nomeArquivo).join(', ');
  }

  protected formatarTamanhoBytes(bytes?: number): string {
    if (!bytes || bytes <= 0) return '0 KB';
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  protected enviarMensagem(): void {
    const texto = this.novaMensagem.trim();
    if (!texto) {
      return;
    }

    this.enviarMensagemClick.emit(texto);
    this.novaMensagem = '';
    this.agendarScrollChat();
  }

  protected registrarDigitacaoChat(): void {
    if (!this.chamadoSelecionado || !this.novaMensagem.trim()) {
      return;
    }

    this.digitandoChat.emit(this.chamadoSelecionado.id);

    localStorage.setItem(
      this.typingStorageKey,
      JSON.stringify({
        chamadoId: this.chamadoSelecionado.id,
        autor: this.usuarioAtualNome,
        expires: Date.now() + 2200,
      })
    );
  }

  protected podeAnexarChamado(): boolean {
    return !!this.chamadoSelecionado && this.chamadoSelecionado.status !== 'Encerrado' && this.chamadoSelecionado.status !== 'Cancelado';
  }

  protected selecionarAnexosChamado(event: Event): void {
    const input = event.target as HTMLInputElement;
    const arquivos = Array.from(input.files ?? []);
    input.value = '';

    if (!this.chamadoSelecionado || !arquivos.length || !this.podeAnexarChamado()) {
      return;
    }

    this.anexarArquivosClick.emit({ chamado: this.chamadoSelecionado, arquivos });
  }

  protected excluirAnexo(anexo: { id?: number; nome: string }): void {
    if (!this.chamadoSelecionado) {
      return;
    }

    this.excluirAnexoClick.emit({ chamado: this.chamadoSelecionado, anexoId: anexo.id, nome: anexo.nome });
  }

  protected visualizarAnexo(anexo: { url?: string }): void {
    if (!anexo.url) {
      return;
    }

    window.open(anexo.url, '_blank', 'noopener,noreferrer');
  }

  private arquivoParaDataUrl(arquivo: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(String(reader.result ?? ''));
      reader.onerror = () => reject(new Error('Nao foi possivel ler o arquivo.'));
      reader.readAsDataURL(arquivo);
    });
  }

  protected publicarComunicado(): void {
    const texto = this.novoComunicado.trim();
    if (!texto) {
      return;
    }

    this.enviarComunicacaoClick.emit(texto);
    this.novoComunicado = '';
    this.paginaComunicados = 0;
  }

  private exibirDigitandoRemoto(): void {
    this.digitandoRemoto = true;
    this.agendarScrollChat();
    window.clearTimeout(this.ocultarDigitandoTimer);
    this.ocultarDigitandoTimer = window.setTimeout(() => {
      this.digitandoRemoto = false;
    }, 2200);
  }

  private readonly processarDigitacaoRemota = (event: StorageEvent): void => {
    if (event.key !== this.typingStorageKey || !event.newValue || !this.chamadoSelecionado) {
      return;
    }

    try {
      const payload = JSON.parse(event.newValue) as { chamadoId?: number; autor?: string; expires?: number };
      const mesmoChamado = payload.chamadoId === this.chamadoSelecionado.id;
      const outroUsuario = !!payload.autor && payload.autor !== this.usuarioAtualNome;
      const ativo = (payload.expires ?? 0) > Date.now();

      if (mesmoChamado && outroUsuario && ativo) {
        this.exibirDigitandoRemoto();
      }
    } catch {
      this.digitandoRemoto = false;
    }
  };

  private prepararEdicao(chamado: Chamado): void {
    this.chamadoEdicao = {
      titulo: chamado.titulo ?? '',
      categoria: chamado.categoria,
      subcategoria: chamado.subcategoria,
      tipo: chamado.tipo,
      prioridade: chamado.prioridade,
      status: chamado.status,
      descricao: chamado.descricao,
    };
  }

  private prepararAvaliacao(chamado: Chamado): void {
    this.avaliacaoSelecionada = chamado.avaliacao ?? 5;
    this.avaliacaoComentario = chamado.avaliacaoComentario ?? '';
  }

  private agendarScrollChat(): void {
    this.forcarScrollChat = true;
    window.setTimeout(() => this.rolarChatParaRodape());
  }

  private rolarChatParaRodape(): void {
    const container = this.chatMessages?.nativeElement;
    if (!container) {
      return;
    }

    requestAnimationFrame(() => {
      container.scrollTop = container.scrollHeight;
    });
  }

  private chaveScrollChat(): string {
    const mensagens = this.mensagensChat();
    const ultima = mensagens[mensagens.length - 1];
    return [
      this.chamadoSelecionado?.id ?? 0,
      mensagens.length,
      ultima?.id ?? '',
      ultima?.timestamp ?? ultima?.enviadoEm ?? ultima?.horario ?? '',
      this.digitandoRemoto ? 'digitando-local' : '',
      this.usuariosDigitandoChat.join('|'),
    ].join(':');
  }

  private criarEdicaoVazia(): Pick<Chamado, 'categoria' | 'subcategoria' | 'tipo' | 'prioridade' | 'status' | 'descricao'> & { titulo: string } {
    return {
      titulo: '',
      categoria: '',
      subcategoria: '',
      tipo: 'Incidente',
      prioridade: 'Media',
      status: 'Aberto',
      descricao: '',
    };
  }

  private movimentoPermitido(statusAtual: Chamado['status'], statusDestino: Chamado['status']): boolean {
    if (statusAtual === 'Encerrado' || statusAtual === 'Cancelado') {
      return false;
    }

    const fluxo: Chamado['status'][] = ['Aberto', 'Em atendimento', 'Aguardando retorno', 'Encerrado', 'Cancelado'];
    const indiceAtual = fluxo.indexOf(statusAtual);
    const indiceDestino = fluxo.indexOf(statusDestino);

    return indiceDestino >= 0;
  }

  private sincronizarCategoriaSelecionada(): void {
    if (!this.categorias.length) {
      return;
    }

    if (!this.categorias.some((categoria) => categoria.nome === this.novoChamado.categoria)) {
      this.definirCategoria(this.categorias[0].nome);
      return;
    }

    const subcategorias = this.subcategoriasCategoriaSelecionada();
    if (subcategorias.length && !subcategorias.includes(this.novoChamado.subcategoria)) {
      this.novoChamado.subcategoria = subcategorias[0];
    }
  }
}
