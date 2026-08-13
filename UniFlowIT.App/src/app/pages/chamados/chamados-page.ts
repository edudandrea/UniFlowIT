import { CommonModule } from '@angular/common';
import { AfterViewChecked, Component, ElementRef, EventEmitter, Input, OnChanges, OnDestroy, Output, SimpleChanges, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CategoriaChamado, Chamado, Mensagem, Perfil, Prioridade } from '../../models/uniflowit.models';

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
  @Input() abrirChamadoModal?: { id: number; aba: 'dados' | 'anexos' | 'avaliacao' | 'comunicacao' | 'chat'; nonce: number } | null;
  @Input() chamadosComAtualizacao: Set<number> = new Set<number>();
  @Input({ required: true }) novoChamado!: {
    titulo: string;
    solicitante: string;
    categoria: string;
    subcategoria: string;
    tipo: Chamado['tipo'];
    prioridade: Prioridade;
    descricao: string;
    anexos: string;
    anexosDetalhes: Array<{ nomeArquivo: string; tipoConteudo: string; tamanhoBytes: number; url: string }>;
  };

  protected novaMensagem = '';
  protected novoComunicado = '';
  protected digitandoRemoto = false;
  protected avaliacaoSelecionada = 5;
  protected avaliacaoComentario = '';
  protected chamadoModalAberto = false;
  protected novoChamadoModalAberto = false;
  protected abaDetalhe: 'dados' | 'anexos' | 'avaliacao' | 'comunicacao' | 'chat' = 'dados';
  protected chamadoEdicao = this.criarEdicaoVazia();
  protected chamadoArrastadoId: number | null = null;
  protected filtroTickets = '';
  protected filtroDataInicio = this.dataIsoSemanaAtual().inicio;
  protected filtroDataFim = this.dataIsoSemanaAtual().fim;
  protected paginaComunicados = 0;
  private readonly comunicadosPorPagina = 3;
  private readonly prioridades: Prioridade[] = ['Baixa', 'Media', 'Alta', 'Urgente'];
  protected readonly colunasKanban: Array<{ titulo: string; status: Chamado['status'] }> = [
    { titulo: 'Pendente', status: 'Aberto' },
    { titulo: 'Em Atendimento', status: 'Em atendimento' },
    { titulo: 'Aguardando Retorno', status: 'Aguardando retorno' },
    { titulo: 'Encerrado', status: 'Encerrado' },
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

  constructor() {
    window.addEventListener('storage', this.processarDigitacaoRemota);
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

  protected abrirDetalheChamado(chamado: Chamado, aba: 'dados' | 'anexos' | 'avaliacao' | 'comunicacao' | 'chat' = 'dados'): void {
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

  protected definirAbaDetalhe(aba: 'dados' | 'anexos' | 'avaliacao' | 'comunicacao' | 'chat'): void {
    this.abaDetalhe = aba;
    if (aba === 'comunicacao') {
      this.paginaComunicados = 0;
    }
    if (aba === 'chat') {
      this.agendarScrollChat();
    }
  }

  protected chamadosPorStatus(status: Chamado['status']): Chamado[] {
    return this.chamadosFiltrados().filter((chamado) => chamado.status === status || (status === 'Encerrado' && chamado.status === 'Cancelado'));
  }

  protected chamadosFiltrados(): Chamado[] {
    const termo = this.filtroTickets.trim().toLocaleLowerCase('pt-BR');
    const inicio = this.filtroDataInicio ? new Date(`${this.filtroDataInicio}T00:00:00`) : null;
    const fim = this.filtroDataFim ? new Date(`${this.filtroDataFim}T23:59:59`) : null;

    return this.chamadosVisiveis.filter((chamado) => {
      const dataChamado = this.dataChamado(chamado);
      const dentroPeriodo = (!inicio || !dataChamado || dataChamado >= inicio) && (!fim || !dataChamado || dataChamado <= fim);
      const texto = [
        chamado.numero,
        chamado.titulo,
        chamado.descricao,
        chamado.categoria,
        chamado.subcategoria,
        chamado.prioridade,
        chamado.atendente,
        chamado.solicitante,
      ].join(' ').toLocaleLowerCase('pt-BR');

      return dentroPeriodo && (!termo || texto.includes(termo));
    });
  }

  protected limparFiltrosTickets(): void {
    const semana = this.dataIsoSemanaAtual();
    this.filtroTickets = '';
    this.filtroDataInicio = semana.inicio;
    this.filtroDataFim = semana.fim;
  }

  protected iniciarArraste(chamado: Chamado): void {
    if (!this.podeEditarChamado() || chamado.status === 'Encerrado') {
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
    return status === 'Aberto' ? 'Pendente' : status === 'Aguardando retorno' ? 'Aguardando Retorno' : status;
  }

  protected chamadoTemAtualizacao(chamado: Chamado): boolean {
    return this.chamadosComAtualizacao.has(chamado.id);
  }

  protected periodoTicketsLabel(): string {
    if (!this.filtroDataInicio && !this.filtroDataFim) {
      return 'Todos os tickets visiveis';
    }

    if (this.filtroDataInicio === this.dataIsoSemanaAtual().inicio && this.filtroDataFim === this.dataIsoSemanaAtual().fim) {
      return 'Tickets da semana';
    }

    return 'Tickets filtrados';
  }

  protected categoriaSelecionada(): CategoriaChamado | undefined {
    return this.categorias.find((categoria) => categoria.nome === this.novoChamado.categoria);
  }

  protected subcategoriasCategoriaSelecionada(): string[] {
    return this.categoriaSelecionada()?.subcategorias ?? [];
  }

  protected alterarCategoria(): void {
    if (!this.categorias.length) {
      return;
    }

    const indiceAtual = this.categorias.findIndex((categoria) => categoria.nome === this.novoChamado.categoria);
    const proxima = this.categorias[(indiceAtual + 1) % this.categorias.length];
    this.definirCategoria(proxima.nome);
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
    return this.perfil === 'Atendente' || this.perfil === 'Administrador';
  }

  protected podeEditarConteudoChamado(): boolean {
    if (!this.chamadoSelecionado || this.chamadoSelecionado.status === 'Encerrado' || this.chamadoSelecionado.status === 'Cancelado') {
      return false;
    }

    return this.podeEditarChamado()
      || this.chamadoSelecionado.solicitanteUsuarioId === this.usuarioAtualId
      || this.chamadoSelecionado.solicitante === this.usuarioAtualNome;
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
  }

  protected reabrirChamado(): void {
    if (!this.chamadoSelecionado || !this.podeReabrirChamado()) {
      return;
    }

    this.salvarChamadoClick.emit({ id: this.chamadoSelecionado.id, status: 'Aberto' });
  }

  protected alternarPrioridade(): void {
    const indiceAtual = this.prioridades.indexOf(this.novoChamado.prioridade);
    this.novoChamado.prioridade = this.prioridades[(indiceAtual + 1) % this.prioridades.length];
  }

  protected nomesAnexos(): string[] {
    return this.novoChamado.anexos
      .split(',')
      .map((anexo) => anexo.trim())
      .filter(Boolean);
  }

  protected async selecionarAnexos(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const arquivos = Array.from(input.files ?? []);
    this.novoChamado.anexos = arquivos.map((arquivo) => arquivo.name).join(', ');
    this.novoChamado.anexosDetalhes = await Promise.all(arquivos.map(async (arquivo) => ({
      nomeArquivo: arquivo.name,
      tipoConteudo: arquivo.type,
      tamanhoBytes: arquivo.size,
      url: await this.arquivoParaDataUrl(arquivo),
    })));
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
      }),
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

  private dataChamado(chamado: Chamado): Date | null {
    const data = Date.parse(chamado.criadoEm ?? chamado.atualizadoEm ?? '');
    return Number.isFinite(data) ? new Date(data) : null;
  }

  private dataIsoSemanaAtual(): { inicio: string; fim: string } {
    const hoje = new Date();
    const diaSemana = hoje.getDay() || 7;
    const inicio = new Date(hoje);
    inicio.setDate(hoje.getDate() - diaSemana + 1);
    const fim = new Date(inicio);
    fim.setDate(inicio.getDate() + 6);

    return {
      inicio: this.dataParaInput(inicio),
      fim: this.dataParaInput(fim),
    };
  }

  private dataParaInput(data: Date): string {
    const ano = data.getFullYear();
    const mes = String(data.getMonth() + 1).padStart(2, '0');
    const dia = String(data.getDate()).padStart(2, '0');
    return `${ano}-${mes}-${dia}`;
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
    if (statusAtual === 'Encerrado') {
      return false;
    }

    const fluxo: Chamado['status'][] = ['Aberto', 'Em atendimento', 'Aguardando retorno', 'Encerrado'];
    const indiceAtual = fluxo.indexOf(statusAtual);
    const indiceDestino = fluxo.indexOf(statusDestino);

    return indiceDestino < indiceAtual || indiceDestino === indiceAtual + 1;
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
