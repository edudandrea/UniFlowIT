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
  @Input() empresaEditando = false;

  @Output() empresaTabChange = new EventEmitter<EmpresaTab>();
  @Output() empresaFiltroChange = new EventEmitter<string>();
  @Output() novaEmpresaClick = new EventEmitter<void>();
  @Output() empresaSelecionadaChange = new EventEmitter<EmpresaLista>();
  @Output() editarEmpresaClick = new EventEmitter<void>();
  @Output() alternarEmpresaAtivaClick = new EventEmitter<void>();
  @Output() fecharModalClick = new EventEmitter<void>();
  @Output() salvarEmpresaSubmit = new EventEmitter<void>();

  protected atualizarCnpj(valor: string): void {
    this.novaEmpresa.cnpj = this.formatarCnpj(valor);
  }

  protected atualizarTelefone(valor: string): void {
    this.novaEmpresa.telefone = this.formatarTelefone(valor);
  }

  private formatarCnpj(valor: string): string {
    const digitos = valor.replace(/\D/g, '').slice(0, 14);
    return digitos
      .replace(/^(\d{2})(\d)/, '$1.$2')
      .replace(/^(\d{2})\.(\d{3})(\d)/, '$1.$2.$3')
      .replace(/\.(\d{3})(\d)/, '.$1/$2')
      .replace(/(\d{4})(\d)/, '$1-$2');
  }

  private formatarTelefone(valor: string): string {
    const digitos = valor.replace(/\D/g, '').slice(0, 11);
    if (digitos.length <= 10) {
      return digitos
        .replace(/^(\d{2})(\d)/, '($1) $2')
        .replace(/(\d{4})(\d)/, '$1-$2');
    }

    return digitos
      .replace(/^(\d{2})(\d)/, '($1) $2')
      .replace(/(\d{5})(\d)/, '$1-$2');
  }
}
