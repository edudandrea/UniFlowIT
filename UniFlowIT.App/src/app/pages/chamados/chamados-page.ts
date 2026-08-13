import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CategoriaChamado, Chamado, Mensagem, Perfil, Prioridade } from '../../models/uniflowit.models';

@Component({
  selector: 'app-chamados-page',
  imports: [CommonModule, FormsModule],
  templateUrl: './chamados-page.html',
  styleUrls: ['./chamados-page.scss'],
})
export class ChamadosPage {
  @Input({ required: true }) perfil!: Perfil;
  @Input() metricas = { abertos: 0, atendimento: 0, urgentes: 0, linksFora: 0 };
  @Input() modoDesenvolvedor = false;
  @Input() categorias: CategoriaChamado[] = [];
  @Input() chamadosVisiveis: Chamado[] = [];
  @Input() chamadoSelecionado?: Chamado;
  @Input() usuarioAtualId?: number;
  @Input() usuarioAtualNome = '';
  @Input({ required: true }) novoChamado!: {
    titulo: string;
    solicitante: string;
    categoria: string;
    subcategoria: string;
    tipo: Chamado['tipo'];
    prioridade: Prioridade;
    descricao: string;
    anexos: string;
  };

  protected novaMensagem = '';
  protected novoComunicado = '';
  protected avaliacaoSelecionada = 5;
  protected chamadoModalAberto = false;
  protected novoChamadoModalAberto = false;
  protected abaDetalhe: 'dados' | 'comunicacao' | 'chat' = 'dados';
  protected chamadoEdicao = this.criarEdicaoVazia();
  protected chamadoArrastadoId: number | null = null;
  private readonly prioridades: Prioridade[] = ['Baixa', 'Media', 'Alta', 'Urgente'];
  protected readonly colunasKanban: Array<{ titulo: string; status: Chamado['status'] }> = [
    { titulo: 'Pendente', status: 'Aberto' },
    { titulo: 'Em Atendimento', status: 'Em atendimento' },
    { titulo: 'Aguardando Retorno', status: 'Aguardando retorno' },
    { titulo: 'Encerrado', status: 'Encerrado' },
  ];

  @Output() abrirChamadoSubmit = new EventEmitter<void>();
  @Output() selecionarChamadoClick = new EventEmitter<number>();
  @Output() salvarChamadoClick = new EventEmitter<Partial<Chamado> & { id: number }>();
  @Output() capturarChamadoClick = new EventEmitter<Chamado>();
  @Output() encerrarChamadoClick = new EventEmitter<Chamado>();
  @Output() cancelarChamadoClick = new EventEmitter<Chamado>();
  @Output() enviarMensagemClick = new EventEmitter<string>();
  @Output() enviarComunicacaoClick = new EventEmitter<string>();
  @Output() movimentoInvalidoClick = new EventEmitter<void>();
  @Output() avaliarChamadoClick = new EventEmitter<{ chamado: Chamado; avaliacao: number }>();

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

  protected abrirDetalheChamado(chamado: Chamado): void {
    this.selecionarChamadoClick.emit(chamado.id);
    this.prepararEdicao(chamado);
    this.abaDetalhe = 'dados';
    this.chamadoModalAberto = true;
  }

  protected fecharDetalheChamado(): void {
    this.chamadoModalAberto = false;
    this.novaMensagem = '';
    this.novoComunicado = '';
  }

  protected definirAbaDetalhe(aba: 'dados' | 'comunicacao' | 'chat'): void {
    this.abaDetalhe = aba;
  }

  protected chamadosPorStatus(status: Chamado['status']): Chamado[] {
    return this.chamadosVisiveis.filter((chamado) => chamado.status === status || (status === 'Encerrado' && chamado.status === 'Cancelado'));
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

  protected podeReabrirChamado(): boolean {
    if (!this.chamadoSelecionado || this.chamadoSelecionado.status !== 'Encerrado' || this.perfil !== 'Usuario') {
      return false;
    }

    return this.chamadoSelecionado.solicitanteUsuarioId === this.usuarioAtualId || this.chamadoSelecionado.solicitante === this.usuarioAtualNome;
  }

  protected comunicacoesMural(): Mensagem[] {
    return this.chamadoSelecionado?.mensagens.filter((mensagem) => mensagem.tipo === 'Mural') ?? [];
  }

  protected mensagensChat(): Mensagem[] {
    return this.chamadoSelecionado?.mensagens.filter((mensagem) => mensagem.tipo !== 'Mural') ?? [];
  }

  protected salvarEdicaoChamado(): void {
    if (!this.chamadoSelecionado || !this.podeEditarChamado()) {
      return;
    }

    this.salvarChamadoClick.emit({
      id: this.chamadoSelecionado.id,
      titulo: this.chamadoEdicao.titulo.trim(),
      categoria: this.chamadoEdicao.categoria,
      subcategoria: this.chamadoEdicao.subcategoria,
      tipo: this.chamadoEdicao.tipo,
      prioridade: this.chamadoEdicao.prioridade,
      status: this.chamadoEdicao.status,
      descricao: this.chamadoEdicao.descricao.trim(),
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

  protected selecionarAnexos(event: Event): void {
    const input = event.target as HTMLInputElement;
    const arquivos = Array.from(input.files ?? []);
    this.novoChamado.anexos = arquivos.map((arquivo) => arquivo.name).join(', ');
  }

  protected enviarMensagem(): void {
    const texto = this.novaMensagem.trim();
    if (!texto) {
      return;
    }

    this.enviarMensagemClick.emit(texto);
    this.novaMensagem = '';
  }

  protected publicarComunicado(): void {
    const texto = this.novoComunicado.trim();
    if (!texto) {
      return;
    }

    this.enviarComunicacaoClick.emit(texto);
    this.novoComunicado = '';
  }

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
