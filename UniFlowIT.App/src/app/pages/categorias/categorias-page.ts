import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CategoriaChamado, CategoriaChamadoForm } from '../../models/uniflowit.models';

@Component({
  selector: 'app-categorias-page',
  imports: [CommonModule, FormsModule],
  templateUrl: './categorias-page.html',
})
export class CategoriasPage {
  @Input() categorias: CategoriaChamado[] = [];
  @Input({ required: true }) categoriaForm!: CategoriaChamadoForm;
  @Input() categoriaModalAberto = false;

  @Output() novaCategoriaClick = new EventEmitter<void>();
  @Output() editarCategoriaClick = new EventEmitter<CategoriaChamado>();
  @Output() fecharModalClick = new EventEmitter<void>();
  @Output() salvarCategoriaSubmit = new EventEmitter<void>();
}
