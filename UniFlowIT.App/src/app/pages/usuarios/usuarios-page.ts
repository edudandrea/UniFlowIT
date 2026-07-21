import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { EmpresaLista, NovoUsuarioForm, UsuarioLista } from '../../models/uniflowit.models';

@Component({
  selector: 'app-usuarios-page',
  imports: [CommonModule, FormsModule],
  templateUrl: './usuarios-page.html',
})
export class UsuariosPage {
  @Input() usuarios: UsuarioLista[] = [];
  @Input() empresas: EmpresaLista[] = [];
  @Input({ required: true }) novoUsuario!: NovoUsuarioForm;
  @Input() usuarioModalAberto = false;
  @Input() travarEmpresa = false;

  @Output() novoUsuarioClick = new EventEmitter<void>();
  @Output() fecharModalClick = new EventEmitter<void>();
  @Output() salvarUsuarioSubmit = new EventEmitter<void>();
}
