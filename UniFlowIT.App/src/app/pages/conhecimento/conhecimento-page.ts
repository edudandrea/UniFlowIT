import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Artigo, CategoriaConhecimento, CategoriaConhecimentoForm, ConhecimentoForm } from '../../models/uniflowit.models';

@Component({
  selector: 'app-conhecimento-page',
  imports: [CommonModule, FormsModule],
  templateUrl: './conhecimento-page.html',
  styleUrls: ['./conhecimento-page.scss'],
})
export class ConhecimentoPage {
  @Input() artigos: Artigo[] = [];
  @Input() categorias: CategoriaConhecimento[] = [];
  @Input({ required: true }) conhecimentoForm!: ConhecimentoForm;
  @Input({ required: true }) categoriaForm!: CategoriaConhecimentoForm;
  @Input() modalConhecimentoAberto = false;
  @Input() modalCategoriaAberto = false;
  @Input() modalListaCategoriasAberto = false;
  @Input() categoriaSelecionada: string | null = null;

  @Output() novoConhecimentoClick = new EventEmitter<void>();
  @Output() novaCategoriaClick = new EventEmitter<void>();
  @Output() editarCategoriaClick = new EventEmitter<CategoriaConhecimento>();
  @Output() abrirCategoriasClick = new EventEmitter<void>();
  @Output() abrirCategoriaClick = new EventEmitter<string>();
  @Output() fecharModaisClick = new EventEmitter<void>();
  @Output() salvarConhecimentoSubmit = new EventEmitter<void>();
  @Output() salvarCategoriaSubmit = new EventEmitter<void>();
  @Output() editarConhecimentoClick = new EventEmitter<Artigo>();
  @Output() excluirConhecimentoClick = new EventEmitter<Artigo>();
  @Output() excluirCategoriaClick = new EventEmitter<CategoriaConhecimentoForm>();

  protected termoPesquisa = '';

  protected atualizarArquivos(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.conhecimentoForm.anexos = Array.from(input.files ?? []).map((arquivo) => arquivo.name);
  }

  protected get categoriasAtivas(): CategoriaConhecimento[] {
    const nomes = new Set<string>();
    const categorias = this.categorias.filter((categoria) => categoria.ativo);

    for (const categoria of categorias) {
      nomes.add(categoria.nome);
    }

    for (const artigo of this.artigos) {
      if (artigo.categoria) {
        nomes.add(artigo.categoria);
      }
    }

    return Array.from(nomes)
      .sort((a, b) => a.localeCompare(b))
      .map((nome) => categorias.find((categoria) => categoria.nome === nome) ?? { nome, ativo: true });
  }

  protected artigosDaCategoria(categoria: string): Artigo[] {
    return this.artigos.filter((artigo) => artigo.categoria === categoria);
  }

  protected totalConhecimentos(categoria: string): number {
    return this.artigosDaCategoria(categoria).length;
  }

  protected get conhecimentosPesquisados(): Artigo[] {
    const termo = this.normalizarPesquisa(this.termoPesquisa);

    if (!termo) {
      return [];
    }

    return this.artigos.filter((artigo) => this.normalizarPesquisa([
      artigo.titulo,
      artigo.categoria,
      artigo.descricao,
      artigo.resumo,
      artigo.usuario,
      ...(artigo.tags ?? []),
      ...(artigo.anexos ?? []),
    ].join(' ')).includes(termo));
  }

  private normalizarPesquisa(valor: string): string {
    return valor
      .toLowerCase()
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .trim();
  }
}
