import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { LinkMonitorado } from '../../models/uniflowit.models';

@Component({
  selector: 'app-links-page',
  imports: [CommonModule],
  templateUrl: './links-page.html',
})
export class LinksPage {
  @Input() links: LinkMonitorado[] = [];

  @Output() alternarLinkClick = new EventEmitter<LinkMonitorado>();
}

