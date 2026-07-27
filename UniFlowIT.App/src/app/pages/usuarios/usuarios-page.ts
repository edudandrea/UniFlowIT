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
  protected senhaVisivel = false;

  @Input() usuarios: UsuarioLista[] = [];
  @Input() empresas: EmpresaLista[] = [];
  @Input({ required: true }) novoUsuario!: NovoUsuarioForm;
  @Input() usuarioModalAberto = false;
  @Input() usuarioEditando = false;
  @Input() travarEmpresa = false;

  @Output() novoUsuarioClick = new EventEmitter<void>();
  @Output() editarUsuarioClick = new EventEmitter<UsuarioLista>();
  @Output() gerarSenhaClick = new EventEmitter<void>();
  @Output() fecharModalClick = new EventEmitter<void>();
  @Output() salvarUsuarioSubmit = new EventEmitter<void>();

  protected exibirSenha(): void {
    this.senhaVisivel = true;
  }

  protected esconderSenha(): void {
    this.senhaVisivel = false;
  }

  protected senhasDiferentes(): boolean {
    return Boolean(this.novoUsuario?.senha || this.novoUsuario?.senhaConfirmacao)
      && this.novoUsuario.senha !== this.novoUsuario.senhaConfirmacao;
  }
}
