import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { EmpresaForm, EmpresaLista, EmpresaTab } from '../../models/uniflowit.models';

@Component({
  selector: 'app-empresas-page',
  imports: [CommonModule, FormsModule],
  templateUrl: './empresas-page.html',
})
export class EmpresasPage {
  @Input({ required: true }) empresasFiltradas: EmpresaLista[] = [];
  @Input({ required: true }) empresaSelecionada!: EmpresaLista;
  @Input({ required: true }) novaEmpresa!: EmpresaForm;
  @Input() empresaTab: EmpresaTab = 'pesquisa';
  @Input() empresaFiltro = '';
  @Input() empresaModalAberto = false;

  @Output() empresaTabChange = new EventEmitter<EmpresaTab>();
  @Output() empresaFiltroChange = new EventEmitter<string>();
  @Output() novaEmpresaClick = new EventEmitter<void>();
  @Output() empresaSelecionadaChange = new EventEmitter<EmpresaLista>();
  @Output() editarEmpresaClick = new EventEmitter<void>();
  @Output() fecharModalClick = new EventEmitter<void>();
  @Output() salvarEmpresaSubmit = new EventEmitter<void>();
}

