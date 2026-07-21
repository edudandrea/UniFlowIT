import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { EnvioEquipamento, Inventario } from '../../models/uniflowit.models';

@Component({
  selector: 'app-equipamentos-page',
  imports: [CommonModule],
  templateUrl: './equipamentos-page.html',
})
export class EquipamentosPage {
  @Input() envios: EnvioEquipamento[] = [];
  @Input() inventario: Inventario[] = [];
}

