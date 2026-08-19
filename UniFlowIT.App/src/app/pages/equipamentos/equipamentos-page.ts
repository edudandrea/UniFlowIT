import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LucideKeyboard, LucideLaptop, LucideMonitor, LucidePackage, LucidePrinter } from '@lucide/angular';
import QRCode from 'qrcode';
import { EmpresaLista, EnvioEquipamento, Inventario, UsuarioLista } from '../../models/uniflowit.models';

type AbaEquipamento = 'pesquisa' | 'cadastro';

@Component({
  selector: 'app-equipamentos-page',
  imports: [CommonModule, FormsModule, LucideKeyboard, LucideLaptop, LucideMonitor, LucidePackage, LucidePrinter],
  templateUrl: './equipamentos-page.html',
  styleUrls: ['./equipamentos-page.scss'],
})
export class EquipamentosPage {
  @Input() envios: EnvioEquipamento[] = [];
  @Input() inventario: Inventario[] = [];
  @Input() empresas: EmpresaLista[] = [];
  @Input() usuarios: UsuarioLista[] = [];
  @Input() exibirIntegracaoRustDesk = false;

  @Output() salvarEquipamentoSubmit = new EventEmitter<Inventario>();
  @Output() excluirEquipamentoClick = new EventEmitter<Inventario>();
  @Output() acessoRemotoClick = new EventEmitter<Inventario>();

  protected aba: AbaEquipamento = 'pesquisa';
  protected pesquisa = '';
  protected modalAberto = false;
  protected modoEdicao = false;
  protected qrCodeUrl = '';
  protected equipamentoForm = this.criarEquipamentoVazio();
  protected readonly tiposEquipamento: Array<NonNullable<Inventario['tipo']>> = ['Computador', 'Notebook', 'Impressora', 'Perifericos', 'Outros'];

  protected usuariosDisponiveis(): UsuarioLista[] {
    return [...this.usuarios].sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR'));
  }

  protected unidadesDisponiveis(): EmpresaLista[] {
    return [...this.empresas].sort((a, b) =>
      (a.nomeFantasia || a.razaoSocial).localeCompare(b.nomeFantasia || b.razaoSocial, 'pt-BR'),
    );
  }

  protected equipamentosFiltrados(): Inventario[] {
    const termo = this.pesquisa.trim().toLocaleLowerCase('pt-BR');
    if (!termo) {
      return this.inventario;
    }

    return this.inventario.filter((item) =>
      [
        item.patrimonio,
        item.hostname,
        item.marca,
        item.modelo,
        item.tipo,
        item.responsavel,
        item.unidade,
        item.filial,
      ]
        .join(' ')
        .toLocaleLowerCase('pt-BR')
        .includes(termo),
    );
  }

  protected abrirNovoEquipamento(): void {
    this.equipamentoForm = this.criarEquipamentoVazio();
    this.modoEdicao = true;
    this.qrCodeUrl = '';
    this.modalAberto = true;
  }

  protected async abrirEquipamento(equipamento: Inventario): Promise<void> {
    this.equipamentoForm = { ...equipamento };
    this.modoEdicao = false;
    this.modalAberto = true;
    this.aba = 'cadastro';
    await this.gerarQrCode();
  }

  protected fecharModal(): void {
    this.modalAberto = false;
    this.modoEdicao = false;
    this.qrCodeUrl = '';
  }

  protected habilitarEdicao(): void {
    this.modoEdicao = true;
  }

  protected podeSelecionarNotaFiscal(): boolean {
    return this.modoEdicao || Boolean(this.equipamentoForm.id);
  }

  protected salvar(): void {
    this.sincronizarRelacionamentos();
    this.salvarEquipamentoSubmit.emit({ ...this.equipamentoForm });
    this.fecharModal();
  }

  protected excluir(): void {
    this.excluirEquipamentoClick.emit({ ...this.equipamentoForm });
    this.fecharModal();
  }

  protected async selecionarNotaFiscal(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const arquivo = input.files?.[0];
    if (!arquivo) {
      return;
    }

    this.modoEdicao = true;
    this.equipamentoForm.notaFiscalNome = arquivo.name;
    this.equipamentoForm.notaFiscalUrl = await this.arquivoParaDataUrl(arquivo);
  }

  protected imprimirQrCode(): void {
    if (!this.qrCodeUrl) {
      return;
    }

    const titulo = this.escapeHtml(this.equipamentoForm.hostname || 'Equipamento');
    const patrimonio = this.escapeHtml(this.equipamentoForm.patrimonio || '');
    const tipo = this.escapeHtml(this.equipamentoForm.tipo || 'Equipamento');
    const unidade = this.escapeHtml(this.equipamentoForm.unidade || this.equipamentoForm.filial || '');
    const janela = window.open('', '_blank', 'width=420,height=560');

    if (!janela) {
      return;
    }

    janela.document.write(`
      <!doctype html>
      <html lang="pt-BR">
        <head>
          <meta charset="utf-8">
          <title>QR Code - ${titulo}</title>
          <style>
            * { box-sizing: border-box; }
            body {
              margin: 0;
              min-height: 100vh;
              display: grid;
              place-items: center;
              font-family: Inter, Arial, sans-serif;
              color: #0f172a;
              background: #ffffff;
            }
            .label {
              width: 86mm;
              min-height: 110mm;
              display: grid;
              justify-items: center;
              align-content: center;
              gap: 10px;
              padding: 10mm;
              border: 1px solid #d8e2ef;
              border-radius: 8px;
              text-align: center;
            }
            img {
              width: 54mm;
              height: 54mm;
              image-rendering: pixelated;
            }
            h1 {
              margin: 4px 0 0;
              font-size: 18px;
              line-height: 1.15;
            }
            p {
              margin: 0;
              font-size: 12px;
              font-weight: 700;
              color: #475569;
            }
            @media print {
              @page { size: auto; margin: 8mm; }
              body { min-height: auto; }
              .label { border-color: #cbd5e1; }
            }
          </style>
        </head>
        <body>
          <main class="label">
            <img src="${this.qrCodeUrl}" alt="QR Code do equipamento">
            <h1>${titulo}</h1>
            <p>${patrimonio}</p>
            <p>${tipo}${unidade ? ` - ${unidade}` : ''}</p>
          </main>
          <script>
            window.addEventListener('load', () => {
              window.focus();
              window.print();
            });
          </script>
        </body>
      </html>
    `);
    janela.document.close();
  }

  protected statusClasse(equipamento: Inventario): string {
    return equipamento.online ? 'online' : 'offline';
  }

  private criarEquipamentoVazio(): Inventario {
    return {
      patrimonio: '',
      hostname: '',
      marca: '',
      modelo: '',
      tipo: 'Computador',
      descricao: '',
      dataCompra: new Date().toISOString().slice(0, 10),
      numeroNotaFiscal: '',
      notaFiscalNome: '',
      notaFiscalUrl: '',
      responsavelUsuarioId: undefined,
      responsavel: '',
      unidadeEmpresaId: this.empresas[0]?.id,
      unidade: this.empresas[0]?.nomeFantasia || this.empresas[0]?.razaoSocial || '',
      online: true,
      usuario: '',
      filial: '',
      sistema: '',
      memoria: '',
      memoriaLivreGb: 0,
      processador: '',
      gpu: '',
      discoGb: 0,
      discoLivreGb: 0,
      discos: [],
      discosJson: '',
      ip: '',
      agentId: '',
      agentVersion: '',
      rustDeskId: '',
    };
  }

  private sincronizarRelacionamentos(): void {
    const usuario = this.usuarios.find((item) => item.id === Number(this.equipamentoForm.responsavelUsuarioId));
    if (usuario) {
      this.equipamentoForm.responsavel = usuario.nome;
      this.equipamentoForm.usuario = usuario.nome;
    }

    const empresa = this.empresas.find((item) => item.id === Number(this.equipamentoForm.unidadeEmpresaId));
    if (empresa) {
      this.equipamentoForm.empresaId = empresa.empresaContratanteId ?? empresa.id;
      this.equipamentoForm.unidade = empresa.nomeFantasia || empresa.razaoSocial;
      this.equipamentoForm.filial = this.equipamentoForm.unidade;
    }
  }

  protected equipamentoDoAgente(): boolean {
    return Boolean(this.equipamentoForm.agentId?.trim());
  }

  protected memoriaTotalGb(): number {
    return Number.parseInt(this.equipamentoForm.memoria || '', 10) || 0;
  }

  protected memoriaUsoPercentual(): number {
    const total = this.memoriaTotalGb();
    const livre = this.equipamentoForm.memoriaLivreGb || 0;
    return total > 0 ? Math.min(100, Math.round(((total - livre) / total) * 100)) : 0;
  }

  protected memoriaUsadaGb(): number {
    return Math.max(0, this.memoriaTotalGb() - (this.equipamentoForm.memoriaLivreGb || 0));
  }

  protected discoUsadoGb(): number {
    const total = this.equipamentoForm.discoGb || 0;
    const livre = this.equipamentoForm.discoLivreGb || 0;
    return Math.max(0, total - livre);
  }

  protected discoUsoPercentual(): number {
    const total = this.equipamentoForm.discoGb || 0;
    return total > 0 ? Math.min(100, Math.round((this.discoUsadoGb() / total) * 100)) : 0;
  }

  protected discosDoAgente(): Array<{ nome: string; unidade: string; totalGb: number; livreGb: number; usadoGb: number; usoPercentual: number }> {
    const discos = this.equipamentoForm.discos?.length
      ? this.equipamentoForm.discos
      : this.parseDiscosJson(this.equipamentoForm.discosJson || '');

    return discos.map((disco) => {
      const totalGb = Number(disco.totalGb || 0);
      const livreGb = Number(disco.livreGb || 0);
      const usadoGb = Math.max(0, totalGb - livreGb);
      return {
        nome: disco.nome || disco.unidade || 'Disco',
        unidade: disco.unidade || '',
        totalGb,
        livreGb,
        usadoGb,
        usoPercentual: totalGb > 0 ? Math.min(100, Math.round((usadoGb / totalGb) * 100)) : 0,
      };
    });
  }

  private parseDiscosJson(valor: string): Array<{ nome: string; unidade: string; totalGb: number; livreGb: number }> {
    if (!valor.trim()) {
      return [];
    }

    try {
      const discos = JSON.parse(valor) as Array<Record<string, unknown>>;
      return Array.isArray(discos)
        ? discos.map((disco) => ({
            nome: String(disco['nome'] ?? disco['Nome'] ?? ''),
            unidade: String(disco['unidade'] ?? disco['Unidade'] ?? ''),
            totalGb: Number(disco['totalGb'] ?? disco['TotalGb'] ?? 0),
            livreGb: Number(disco['livreGb'] ?? disco['LivreGb'] ?? 0),
          }))
        : [];
    } catch {
      return [];
    }
  }

  private async gerarQrCode(): Promise<void> {
    const apiBase = (window.location.origin || 'http://localhost:4200').replace(/:\d+$/, ':5151');
    const url = `${apiBase}/api/equipamentos/publico/${encodeURIComponent(this.equipamentoForm.patrimonio)}/pagina`;
    this.qrCodeUrl = await QRCode.toDataURL(url, { margin: 1, width: 180 });
  }

  private arquivoParaDataUrl(arquivo: File): Promise<string> {
    return new Promise((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () => resolve(String(reader.result ?? ''));
      reader.onerror = () => reject(new Error('Nao foi possivel ler o arquivo.'));
      reader.readAsDataURL(arquivo);
    });
  }

  private escapeHtml(valor: string): string {
    return valor.replace(/[&<>"']/g, (char) => ({
      '&': '&amp;',
      '<': '&lt;',
      '>': '&gt;',
      '"': '&quot;',
      "'": '&#039;',
    })[char] ?? char);
  }
}
