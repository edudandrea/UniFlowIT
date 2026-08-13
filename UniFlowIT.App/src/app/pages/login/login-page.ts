import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgxSpinnerModule } from 'ngx-spinner';
import { AdminSaasForm, AuthMode, LoginForm } from '../../models/uniflowit.models';

@Component({
  selector: 'app-login-page',
  imports: [CommonModule, FormsModule, NgxSpinnerModule],
  templateUrl: './login-page.html',
  styleUrls: ['./login-page.scss'],
})
export class LoginPage {
  @Input({ required: true }) authMode!: AuthMode;
  @Input({ required: true }) loginForm!: LoginForm;
  @Input({ required: true }) adminSaasForm!: AdminSaasForm;
  @Input() existeAdministradorSaas = false;
  @Input() carregandoAuth = false;
  @Input() authFeedback = '';

  @Output() authModeChange = new EventEmitter<AuthMode>();
  @Output() loginSubmit = new EventEmitter<void>();
  @Output() adminSaasSubmit = new EventEmitter<void>();
}
