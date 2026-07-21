import { CommonModule } from '@angular/common';
import { Component, Input } from '@angular/core';
import { Artigo } from '../../models/uniflowit.models';

@Component({
  selector: 'app-conhecimento-page',
  imports: [CommonModule],
  templateUrl: './conhecimento-page.html',
})
export class ConhecimentoPage {
  @Input() artigos: Artigo[] = [];
}

