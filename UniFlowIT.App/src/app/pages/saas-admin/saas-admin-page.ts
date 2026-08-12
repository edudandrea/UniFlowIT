import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import QRCode from 'qrcode';
import {
  AssinaturaSaas,
  Chamado,
  CobrancaSaas,
  DadosEmpresaSaas,
  DespesaSaas,
  EmpresaLista,
  FormaPagamentoSaas,
  Inventario,
  Pagina,
  PlanoSaas,
  UsuarioLista,
} from '../../models/uniflowit.models';

@Component({
  selector: 'app-saas-admin-page',
  imports: [CommonModule, FormsModule],
  templateUrl: './saas-admin-page.html',
})
export class SaasAdminPage implements OnChanges {
  @Input({ required: true }) pagina!: Pagina;
  @Input() empresas: EmpresaLista[] = [];
  @Input() usuarios: UsuarioLista[] = [];
  @Input() chamados: Chamado[] = [];
  @Input() inventario: Inventario[] = [];
  @Input({ required: true }) dadosEmpresa!: DadosEmpresaSaas;
  @Input({ required: true }) planos: PlanoSaas[] = [];
  @Input({ required: true }) planoForm!: PlanoSaas;
  @Input({ required: true }) assinaturas: AssinaturaSaas[] = [];
  @Input({ required: true }) assinaturaForm!: AssinaturaSaas;
  @Input({ required: true }) cobrancas: CobrancaSaas[] = [];
  @Input({ required: true }) cobrancaForm!: CobrancaSaas;
  @Input({ required: true }) formasPagamento: FormaPagamentoSaas[] = [];
  @Input({ required: true }) formaPagamentoForm!: FormaPagamentoSaas;
  @Input({ required: true }) despesas: DespesaSaas[] = [];
  @Input({ required: true }) despesaForm!: DespesaSaas;

  @Output() salvarDadosEmpresa = new EventEmitter<void>();
  @Output() salvarPlano = new EventEmitter<void>();
  @Output() salvarAssinatura = new EventEmitter<void>();
  @Output() salvarCobranca = new EventEmitter<void>();
  @Output() salvarFormaPagamento = new EventEmitter<void>();
  @Output() salvarDespesa = new EventEmitter<void>();

  protected pixPayload = '';
  protected pixQrCodeUrl = '';

  protected clientesAtivos(): number {
    return this.empresas.filter((empresa) => empresa.ativo && !empresa.acessoBloqueado).length;
  }

  protected mrr(): number {
    return this.assinaturas
      .filter((assinatura) => assinatura.status === 'Ativa')
      .reduce((total, assinatura) => total + Number(assinatura.valorMensal || 0), 0);
  }

  protected inadimplencia(): number {
    return this.cobrancas
      .filter((cobranca) => cobranca.status === 'Vencida')
      .reduce((total, cobranca) => total + Number(cobranca.valor || 0), 0);
  }

  protected disponibilidade(): string {
    return this.empresas.length ? '100%' : '0%';
  }

  protected churn(): string {
    const total = this.assinaturas.length;
    if (!total) {
      return '0%';
    }

    const canceladas = this.assinaturas.filter((assinatura) => assinatura.status === 'Cancelada').length;
    return `${((canceladas / total) * 100).toFixed(1).replace('.', ',')}%`;
  }

  protected formatarMoeda(valor: number): string {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(valor);
  }

  protected evolucaoReceita(): Array<{ label: string; valor: number; altura: number }> {
    return this.evolucaoPorMes(this.cobrancas.filter((cobranca) => cobranca.status === 'Paga'), 'vencimento', 'valor');
  }

  protected evolucaoChamados(): Array<{ label: string; valor: number; altura: number }> {
    const mapa = new Map<string, number>();
    this.chamados.forEach((chamado) => {
      const chave = chamado.numero.match(/CH-(\d{4})(\d{2})/)?.slice(1, 3).join('-') ?? 'Sem data';
      mapa.set(chave, (mapa.get(chave) ?? 0) + 1);
    });

    return this.normalizarSerie(mapa);
  }

  async ngOnChanges(changes: SimpleChanges): Promise<void> {
    if (changes['formaPagamentoForm'] || changes['cobrancaForm'] || changes['pagina']) {
      await this.atualizarPix();
    }
  }

  protected empresaNome(id: number): string {
    const empresa = this.empresas.find((item) => item.id === Number(id));
    return empresa?.nomeFantasia || empresa?.razaoSocial || 'Empresa nao localizada';
  }

  protected planoNome(id: number): string {
    return this.planos.find((item) => item.id === Number(id))?.nome ?? 'Plano nao localizado';
  }

  protected assinaturaDescricao(id: number): string {
    const assinatura = this.assinaturas.find((item) => item.id === Number(id));
    return assinatura ? `${this.empresaNome(assinatura.empresaId)} - ${this.planoNome(assinatura.planoId)}` : 'Assinatura nao localizada';
  }

  protected paginaGenericaSaas(): boolean {
    return this.pagina.startsWith('saas-') && this.pagina !== 'saas-dashboard';
  }

  protected tituloGenerico(): string {
    const titulos: Partial<Record<Pagina, string>> = {
      'saas-implantacoes': 'Implantacoes',
      'saas-inadimplencia': 'Inadimplencia',
      'saas-chamados-globais': 'Tickets globais',
      'saas-sla-plataforma': 'SLA da plataforma',
      'saas-incidentes': 'Incidentes',
      'saas-monitoramento': 'Monitoramento SaaS',
      'saas-agentes': 'Agentes',
      'saas-integracoes': 'Integracoes',
      'saas-acesso-remoto': 'Acesso remoto',
      'saas-relatorios': 'Relatorios',
      'saas-metricas-uso': 'Metricas de uso',
      'saas-auditoria': 'Auditoria',
      'saas-configuracoes': 'Configuracoes',
      'saas-seguranca': 'Seguranca',
      'saas-administradores': 'Administradores',
    };

    return titulos[this.pagina] ?? 'Administracao SaaS';
  }

  protected async salvarPagamento(): Promise<void> {
    await this.atualizarPix();
    this.salvarFormaPagamento.emit();
  }

  private evolucaoPorMes<T>(itens: T[], campoData: keyof T, campoValor: keyof T): Array<{ label: string; valor: number; altura: number }> {
    const mapa = new Map<string, number>();
    itens.forEach((item) => {
      const data = String(item[campoData] ?? '');
      const chave = data.slice(0, 7) || 'Sem data';
      mapa.set(chave, (mapa.get(chave) ?? 0) + Number(item[campoValor] ?? 0));
    });

    return this.normalizarSerie(mapa);
  }

  private normalizarSerie(mapa: Map<string, number>): Array<{ label: string; valor: number; altura: number }> {
    const itens = Array.from(mapa.entries())
      .sort(([a], [b]) => a.localeCompare(b))
      .slice(-6);
    const maior = Math.max(1, ...itens.map(([, valor]) => valor));
    return itens.map(([label, valor]) => ({ label, valor, altura: Math.max(8, Math.round((valor / maior) * 100)) }));
  }

  protected async atualizarPix(): Promise<void> {
    const forma = this.formaPagamentoForm;
    if (!forma?.chavePix?.trim()) {
      this.pixPayload = '';
      this.pixQrCodeUrl = '';
      return;
    }

    this.pixPayload = this.criarPayloadPix({
      chave: forma.chavePix,
      nome: forma.recebedorPix || this.dadosEmpresa?.nomeFantasia || 'UNIFLOWIT',
      cidade: forma.cidadePix || this.dadosEmpresa?.cidade || 'SAO PAULO',
      valor: Number(this.cobrancaForm?.valor || 0),
      txid: `UNI${String(this.cobrancaForm?.id || 1).padStart(6, '0')}`,
    });
    this.pixQrCodeUrl = await QRCode.toDataURL(this.pixPayload, { margin: 1, width: 220 });
  }

  private criarPayloadPix(dados: { chave: string; nome: string; cidade: string; valor: number; txid: string }): string {
    const merchantAccount = this.campo('00', 'br.gov.bcb.pix') + this.campo('01', dados.chave.trim());
    const additionalData = this.campo('05', this.limitar(this.normalizar(dados.txid), 25));
    const valor = dados.valor > 0 ? this.campo('54', dados.valor.toFixed(2)) : '';
    const payloadSemCrc =
      this.campo('00', '01') +
      this.campo('26', merchantAccount) +
      this.campo('52', '0000') +
      this.campo('53', '986') +
      valor +
      this.campo('58', 'BR') +
      this.campo('59', this.limitar(this.normalizar(dados.nome), 25)) +
      this.campo('60', this.limitar(this.normalizar(dados.cidade), 15)) +
      this.campo('62', additionalData) +
      '6304';

    return `${payloadSemCrc}${this.crc16(payloadSemCrc)}`;
  }

  private campo(id: string, valor: string): string {
    return `${id}${String(valor.length).padStart(2, '0')}${valor}`;
  }

  private normalizar(valor: string): string {
    return valor.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toUpperCase();
  }

  private limitar(valor: string, tamanho: number): string {
    return valor.slice(0, tamanho);
  }

  private crc16(payload: string): string {
    let crc = 0xffff;
    for (let i = 0; i < payload.length; i++) {
      crc ^= payload.charCodeAt(i) << 8;
      for (let bit = 0; bit < 8; bit++) {
        crc = crc & 0x8000 ? (crc << 1) ^ 0x1021 : crc << 1;
      }
    }

    return (crc & 0xffff).toString(16).toUpperCase().padStart(4, '0');
  }
}
