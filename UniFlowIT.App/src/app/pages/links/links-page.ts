import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LinkMonitorado } from '../../models/uniflowit.models';

interface LinkSubmitEvent {
  link: LinkMonitorado;
  originalNome?: string;
}

interface MapTile {
  url: string;
  style: Record<string, string>;
}

interface MapConnection {
  id: string;
  path: string;
  down: boolean;
}

interface CepEndereco {
  cep?: string;
  street?: string;
  neighborhood?: string;
  city?: string;
  state?: string;
  location?: {
    coordinates?: {
      latitude?: string;
      longitude?: string;
    };
  };
}

@Component({
  selector: 'app-links-page',
  imports: [CommonModule, FormsModule],
  templateUrl: './links-page.html',
  styleUrl: './links-page.scss',
})
export class LinksPage {
  @Input() links: LinkMonitorado[] = [];
  @Input() linkForm: LinkMonitorado = {
    nome: '',
    tipo: 'Link internet',
    local: 'Matriz',
    firewall: '',
    endereco: '',
    cep: '',
    intervalo: 30,
    pingMs: 25,
    latitude: -23.561,
    longitude: -46.656,
    disponivel: true,
  };

  @Output() alternarLinkClick = new EventEmitter<LinkMonitorado>();
  @Output() salvarLinkSubmit = new EventEmitter<LinkSubmitEvent>();
  @Output() excluirLinkClick = new EventEmitter<LinkMonitorado>();

  protected readonly tipos: LinkMonitorado['tipo'][] = ['Link internet', 'Firewall', 'Site'];
  protected readonly locais: LinkMonitorado['local'][] = ['Matriz', 'Filial', 'Site externo'];
  protected readonly intervalos = [5, 10, 30, 60];
  protected linkPesquisa = '';
  protected linkEditando = false;
  protected linkSelecionadoNome?: string;
  protected listaLinksModalAberto = false;
  protected novoLinkModalAberto = false;
  protected novoLinkForm: LinkMonitorado = this.linkPadrao();
  protected buscandoCepEdicao = false;
  protected buscandoCepNovo = false;
  protected mapaZoom = 13;
  protected mapaArrastando = false;
  private mapaCentroManual?: { latitude: number; longitude: number };
  private mapaArrastoInicio?: { x: number; y: number; centro: { latitude: number; longitude: number } };
  private cepBuscaAtual = '';

  protected get linksFiltrados(): LinkMonitorado[] {
    const filtro = this.linkPesquisa.trim().toLowerCase();

    if (!filtro) {
      return this.links;
    }

    return this.links.filter((link) =>
      [link.nome, link.tipo, link.local, link.firewall, link.endereco]
        .some((valor) => valor.toLowerCase().includes(filtro)),
    );
  }

  protected get linksAtivos(): LinkMonitorado[] {
    return this.links.filter((link) => link.disponivel);
  }

  protected get linksInativos(): LinkMonitorado[] {
    return this.links.filter((link) => !link.disponivel);
  }

  protected get menorPing(): LinkMonitorado | undefined {
    return this.linksAtivos.reduce<LinkMonitorado | undefined>((menor, link) => (!menor || link.pingMs < menor.pingMs ? link : menor), undefined);
  }

  protected get maiorPing(): LinkMonitorado | undefined {
    return this.linksAtivos.reduce<LinkMonitorado | undefined>((maior, link) => (!maior || link.pingMs > maior.pingMs ? link : maior), undefined);
  }

  protected get linksOrdenados(): LinkMonitorado[] {
    return [...this.links].sort((a, b) => {
      if (a.disponivel !== b.disponivel) {
        return a.disponivel ? 1 : -1;
      }

      return b.pingMs - a.pingMs;
    });
  }

  protected get linksMapa(): LinkMonitorado[] {
    return this.links.filter((link) => link.tipo === 'Firewall' && Number.isFinite(link.latitude) && Number.isFinite(link.longitude));
  }

  protected conexoesMapa(): MapConnection[] {
    const matriz = this.linksMapa.find((link) => link.local === 'Matriz') ?? this.linksMapa[0];

    if (!matriz) {
      return [];
    }

    return this.linksMapa
      .filter((link) => link !== matriz)
      .map((filial) => {
        const origem = this.mapPoint(matriz);
        const destino = this.mapPoint(filial);
        const meioX = (origem.x + destino.x) / 2;
        const meioY = (origem.y + destino.y) / 2;
        const dx = destino.x - origem.x;
        const dy = destino.y - origem.y;
        const distancia = Math.hypot(dx, dy) || 1;
        const curvatura = Math.min(18, Math.max(8, distancia * 0.22));
        const controleX = meioX - dy / distancia * curvatura;
        const controleY = meioY + dx / distancia * curvatura - curvatura * 0.45;

        return {
          id: `${matriz.nome}-${filial.nome}`,
          path: `M ${origem.x.toFixed(2)} ${origem.y.toFixed(2)} Q ${controleX.toFixed(2)} ${controleY.toFixed(2)} ${destino.x.toFixed(2)} ${destino.y.toFixed(2)}`,
          down: !matriz.disponivel || !filial.disponivel,
        };
      });
  }

  protected mapStyle(link: LinkMonitorado): Record<string, string> {
    const point = this.mapPoint(link);

    return {
      left: `${point.x}%`,
      top: `${point.y}%`,
    };
  }

  protected pingLabel(link?: LinkMonitorado): string {
    if (!link) {
      return '-';
    }

    return link.disponivel ? `${link.pingMs} ms` : 'Offline';
  }

  protected mapTiles(): MapTile[] {
    const viewport = this.mapViewport();
    const tileSize = 256;
    const startX = Math.floor(viewport.left / tileSize);
    const endX = Math.floor((viewport.left + viewport.width) / tileSize);
    const startY = Math.floor(viewport.top / tileSize);
    const endY = Math.floor((viewport.top + viewport.height) / tileSize);
    const tiles: MapTile[] = [];

    for (let x = startX; x <= endX; x += 1) {
      for (let y = startY; y <= endY; y += 1) {
        tiles.push({
          url: `https://tile.openstreetmap.org/${viewport.zoom}/${x}/${y}.png`,
          style: {
            left: `${((x * tileSize - viewport.left) / viewport.width) * 100}%`,
            top: `${((y * tileSize - viewport.top) / viewport.height) * 100}%`,
            width: `${(tileSize / viewport.width) * 100}%`,
            height: `${(tileSize / viewport.height) * 100}%`,
          },
        });
      }
    }

    return tiles;
  }

  protected aumentarZoom(): void {
    this.mapaZoom = Math.min(18, this.mapaZoom + 1);
    this.mapaCentroManual ??= this.mapCenter();
  }

  protected diminuirZoom(): void {
    this.mapaZoom = Math.max(5, this.mapaZoom - 1);
    this.mapaCentroManual ??= this.mapCenter();
  }

  protected zoomMapaComScroll(event: WheelEvent): void {
    event.preventDefault();

    const proximoZoom = Math.min(18, Math.max(5, this.mapaZoom + (event.deltaY < 0 ? 1 : -1)));

    if (proximoZoom === this.mapaZoom) {
      return;
    }

    const alvo = event.currentTarget as HTMLElement;
    const rect = alvo.getBoundingClientRect();
    const relX = (event.clientX - rect.left) / rect.width;
    const relY = (event.clientY - rect.top) / rect.height;
    const viewportAtual = this.mapViewport();
    const pixelSobCursor = {
      x: viewportAtual.left + relX * viewportAtual.width,
      y: viewportAtual.top + relY * viewportAtual.height,
    };
    const coordenadaSobCursor = this.pixelToLatLng(pixelSobCursor.x, pixelSobCursor.y, this.mapaZoom);
    const novoPixelSobCursor = this.latLngToPixel(coordenadaSobCursor.latitude, coordenadaSobCursor.longitude, proximoZoom);
    const novoCentroPixel = {
      x: novoPixelSobCursor.x - (relX - 0.5) * viewportAtual.width,
      y: novoPixelSobCursor.y - (relY - 0.5) * viewportAtual.height,
    };

    this.mapaZoom = proximoZoom;
    this.mapaCentroManual = this.pixelToLatLng(novoCentroPixel.x, novoCentroPixel.y, proximoZoom);
  }

  protected iniciarArrastoMapa(event: MouseEvent): void {
    if (event.button !== 0 || (event.target as HTMLElement).closest('.map-pin, .map-zoom-controls')) {
      return;
    }

    event.preventDefault();
    this.mapaArrastando = true;
    this.mapaCentroManual ??= this.mapCenter();
    this.mapaArrastoInicio = {
      x: event.clientX,
      y: event.clientY,
      centro: this.mapaCentroManual,
    };
  }

  protected arrastarMapa(event: MouseEvent): void {
    if (!this.mapaArrastando || !this.mapaArrastoInicio) {
      return;
    }

    event.preventDefault();
    const viewport = this.mapViewport(this.mapaArrastoInicio.centro);
    const centroPixel = this.latLngToPixel(this.mapaArrastoInicio.centro.latitude, this.mapaArrastoInicio.centro.longitude, this.mapaZoom);
    const novoCentroPixel = {
      x: centroPixel.x - (event.clientX - this.mapaArrastoInicio.x) * (viewport.width / Math.max(1, (event.currentTarget as HTMLElement).clientWidth)),
      y: centroPixel.y - (event.clientY - this.mapaArrastoInicio.y) * (viewport.height / Math.max(1, (event.currentTarget as HTMLElement).clientHeight)),
    };

    this.mapaCentroManual = this.pixelToLatLng(novoCentroPixel.x, novoCentroPixel.y, this.mapaZoom);
  }

  protected finalizarArrastoMapa(): void {
    this.mapaArrastando = false;
    this.mapaArrastoInicio = undefined;
  }

  protected abrirNovoLink(): void {
    this.novoLinkForm = this.linkPadrao();
    this.novoLinkModalAberto = true;
  }

  protected fecharListaLinks(): void {
    this.listaLinksModalAberto = false;
    this.linkEditando = false;
  }

  protected fecharNovoLink(): void {
    this.novoLinkModalAberto = false;
  }

  protected abrirEdicaoLink(link: LinkMonitorado): void {
    this.listaLinksModalAberto = true;
    this.selecionarLink(link);
  }

  protected selecionarLink(link: LinkMonitorado): void {
    Object.assign(this.linkForm, { ...link });
    this.linkSelecionadoNome = link.nome;
    this.linkEditando = false;
  }

  protected editarLink(): void {
    this.linkEditando = true;
  }

  protected salvarLink(): void {
    if (!this.linkSelecionadoNome) {
      return;
    }

    const originalNome = this.linkSelecionadoNome;
    void this.resolverCoordenadasAntesDeSalvar(this.linkForm, 'edicao').then(() => {
      this.salvarLinkSubmit.emit({ link: { ...this.linkForm }, originalNome });
      this.linkSelecionadoNome = this.linkForm.nome;
      this.linkEditando = false;
      this.fecharListaLinks();
    });
  }

  protected excluirLink(): void {
    if (!this.linkSelecionadoNome) {
      return;
    }

    this.excluirLinkClick.emit({ ...this.linkForm });
    this.fecharListaLinks();
  }

  protected cadastrarNovoLink(): void {
    void this.resolverCoordenadasAntesDeSalvar(this.novoLinkForm, 'novo').then(() => {
      this.salvarLinkSubmit.emit({ link: { ...this.novoLinkForm } });
      this.novoLinkModalAberto = false;
    });
  }

  protected atualizarCep(form: LinkMonitorado, valor: string, origem: 'novo' | 'edicao'): void {
    form.cep = this.formatarCep(valor);

    if (form.cep.replace(/\D/g, '').length === 8) {
      void this.buscarCoordenadasPorCep(form, origem);
    }
  }

  protected async buscarCoordenadasPorCep(form: LinkMonitorado, origem: 'novo' | 'edicao'): Promise<void> {
    const cep = form.cep.replace(/\D/g, '');

    if (cep.length !== 8) {
      return;
    }

    if (origem === 'novo') {
      this.buscandoCepNovo = true;
    } else {
      this.buscandoCepEdicao = true;
    }

    try {
      this.cepBuscaAtual = `${origem}:${cep}`;
      const response = await fetch(`https://brasilapi.com.br/api/cep/v2/${cep}`);
      const data = response.ok ? await response.json() as CepEndereco : await this.buscarEnderecoViaCep(cep);
      let coordenadas = await this.geocodificarCep(cep, data);
      const latitudeBrasilApi = Number(data.location?.coordinates?.latitude);
      const longitudeBrasilApi = Number(data.location?.coordinates?.longitude);

      if (this.cepBuscaAtual !== `${origem}:${cep}`) {
        return;
      }

      form.cep = this.formatarCep(data.cep ?? cep);

      if (!coordenadas && Number.isFinite(latitudeBrasilApi) && Number.isFinite(longitudeBrasilApi)) {
        coordenadas = { latitude: latitudeBrasilApi, longitude: longitudeBrasilApi };
      }

      if (coordenadas) {
        form.latitude = Number(coordenadas.latitude.toFixed(6));
        form.longitude = Number(coordenadas.longitude.toFixed(6));
        this.centralizarMapaEm(form.latitude, form.longitude);
      }
    } finally {
      if (origem === 'novo') {
        this.buscandoCepNovo = false;
      } else {
        this.buscandoCepEdicao = false;
      }
    }
  }

  private mapCenter(): { latitude: number; longitude: number } {
    if (this.mapaCentroManual) {
      return this.mapaCentroManual;
    }

    const linksComCoordenadas = this.linksMapa.length ? this.linksMapa : this.links.filter((link) => Number.isFinite(link.latitude) && Number.isFinite(link.longitude));
    const base = linksComCoordenadas.length ? linksComCoordenadas : [{ latitude: -23.561, longitude: -46.656 } as LinkMonitorado];

    return {
      latitude: base.reduce((total, link) => total + link.latitude, 0) / base.length,
      longitude: base.reduce((total, link) => total + link.longitude, 0) / base.length,
    };
  }

  private mapViewport(centro = this.mapCenter()): { zoom: number; left: number; top: number; width: number; height: number } {
    const zoom = this.mapaZoom;
    const centerPixel = this.latLngToPixel(centro.latitude, centro.longitude, zoom);
    const width = 1280;
    const height = 620;

    return {
      zoom,
      left: centerPixel.x - width / 2,
      top: centerPixel.y - height / 2,
      width,
      height,
    };
  }

  private latLngToPixel(latitude: number, longitude: number, zoom: number): { x: number; y: number } {
    const sinLatitude = Math.sin(latitude * Math.PI / 180);
    const scale = 256 * 2 ** zoom;

    return {
      x: ((longitude + 180) / 360) * scale,
      y: (0.5 - Math.log((1 + sinLatitude) / (1 - sinLatitude)) / (4 * Math.PI)) * scale,
    };
  }

  private pixelToLatLng(x: number, y: number, zoom: number): { latitude: number; longitude: number } {
    const scale = 256 * 2 ** zoom;
    const longitude = x / scale * 360 - 180;
    const n = Math.PI - 2 * Math.PI * y / scale;
    const latitude = 180 / Math.PI * Math.atan(0.5 * (Math.exp(n) - Math.exp(-n)));

    return { latitude, longitude };
  }

  private mapPoint(link: LinkMonitorado): { x: number; y: number } {
    const viewport = this.mapViewport();
    const point = this.latLngToPixel(link.latitude, link.longitude, viewport.zoom);
    const left = ((point.x - viewport.left) / viewport.width) * 100;
    const top = ((point.y - viewport.top) / viewport.height) * 100;

    return {
      x: Math.min(96, Math.max(4, left)),
      y: Math.min(96, Math.max(4, top)),
    };
  }

  private async buscarEnderecoViaCep(cep: string): Promise<CepEndereco> {
    const response = await fetch(`https://viacep.com.br/ws/${cep}/json/`);

    if (!response.ok) {
      return { cep };
    }

    const data = await response.json() as { cep?: string; logradouro?: string; bairro?: string; localidade?: string; uf?: string };

    return {
      cep: data.cep ?? cep,
      street: data.logradouro ?? '',
      neighborhood: data.bairro ?? '',
      city: data.localidade ?? '',
      state: data.uf ?? '',
    };
  }

  private async geocodificarCep(cep: string, endereco: CepEndereco): Promise<{ latitude: number; longitude: number } | null> {
    const params = new URLSearchParams({
      format: 'json',
      limit: '1',
      countrycodes: 'br',
      postalcode: cep,
      country: 'Brasil',
    });

    if (endereco.street) {
      params.set('street', endereco.street);
    }

    if (endereco.city) {
      params.set('city', endereco.city);
    }

    if (endereco.state) {
      params.set('state', endereco.state);
    }

    let response = await fetch(`https://nominatim.openstreetmap.org/search?${params.toString()}`);

    if (!response.ok) {
      return null;
    }

    let resultado = await response.json() as Array<{ lat?: string; lon?: string }>;

    if (!resultado.length) {
      const query = [
        endereco.street,
        endereco.neighborhood,
        endereco.city,
        endereco.state,
        cep,
        'Brasil',
      ].filter(Boolean).join(', ');
      response = await fetch(`https://nominatim.openstreetmap.org/search?format=json&limit=1&countrycodes=br&q=${encodeURIComponent(query)}`);

      if (!response.ok) {
        return null;
      }

      resultado = await response.json() as Array<{ lat?: string; lon?: string }>;
    }

    const primeiro = resultado[0];
    const latitude = Number(primeiro?.lat);
    const longitude = Number(primeiro?.lon);

    return Number.isFinite(latitude) && Number.isFinite(longitude) ? { latitude, longitude } : null;
  }

  private centralizarMapaEm(latitude: number, longitude: number): void {
    this.mapaCentroManual = { latitude, longitude };
    this.mapaZoom = Math.max(this.mapaZoom, 16);
  }

  private async resolverCoordenadasAntesDeSalvar(form: LinkMonitorado, origem: 'novo' | 'edicao'): Promise<void> {
    const cep = form.cep.replace(/\D/g, '');

    if (cep.length === 8) {
      await this.buscarCoordenadasPorCep(form, origem);
    }
  }

  private linkPadrao(): LinkMonitorado {
    return {
      nome: '',
      tipo: 'Link internet',
      local: 'Matriz',
      firewall: '',
      endereco: '',
      cep: '',
      intervalo: 30,
      pingMs: 25,
      latitude: -23.561,
      longitude: -46.656,
      disponivel: true,
    };
  }

  private formatarCep(cep: string): string {
    const numeros = cep.replace(/\D/g, '').slice(0, 8);
    return numeros.length > 5 ? `${numeros.slice(0, 5)}-${numeros.slice(5)}` : numeros;
  }
}
