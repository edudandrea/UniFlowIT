import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CategoriaChamado, Chamado, Perfil, Prioridade } from '../../models/uniflowit.models';

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
  protected avaliacaoSelecionada = 5;
  protected chamadoModalAberto = false;
  protected novoChamadoModalAberto = false;
  protected abaDetalhe: 'dados' | 'comunicacao' | 'chat' = 'dados';
  private readonly prioridades: Prioridade[] = ['Baixa', 'Media', 'Alta', 'Urgente'];

  @Output() abrirChamadoSubmit = new EventEmitter<void>();
  @Output() selecionarChamadoClick = new EventEmitter<number>();
  @Output() capturarChamadoClick = new EventEmitter<Chamado>();
  @Output() encerrarChamadoClick = new EventEmitter<Chamado>();
  @Output() cancelarChamadoClick = new EventEmitter<Chamado>();
  @Output() enviarMensagemClick = new EventEmitter<string>();
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
    this.abaDetalhe = 'dados';
    this.chamadoModalAberto = true;
  }

  protected fecharDetalheChamado(): void {
    this.chamadoModalAberto = false;
    this.novaMensagem = '';
  }

  protected definirAbaDetalhe(aba: 'dados' | 'comunicacao' | 'chat'): void {
    this.abaDetalhe = aba;
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
