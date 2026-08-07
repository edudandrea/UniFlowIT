import { CommonModule } from '@angular/common';
import { Component, HostBinding, OnInit, ViewEncapsulation, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgxSpinnerModule, NgxSpinnerService } from 'ngx-spinner';
import { ToastrService } from 'ngx-toastr';
import { ChamadosPage } from './pages/chamados/chamados-page';
import { CategoriasPage } from './pages/categorias/categorias-page';
import { ConhecimentoPage } from './pages/conhecimento/conhecimento-page';
import { DashboardPage } from './pages/dashboard/dashboard-page';
import { EmpresasPage } from './pages/empresas/empresas-page';
import { EquipamentosPage } from './pages/equipamentos/equipamentos-page';
import { LinksPage } from './pages/links/links-page';
import { LoginPage } from './pages/login/login-page';
import { SaasAdminPage } from './pages/saas-admin/saas-admin-page';
import { UsuariosPage } from './pages/usuarios/usuarios-page';
import {
  Artigo,
  AuthMode,
  AssinaturaSaas,
  CategoriaChamado,
  CategoriaChamadoForm,
  Chamado,
  CobrancaSaas,
  DadosEmpresaSaas,
  DespesaSaas,
  EmpresaForm,
  EmpresaLista,
  EmpresaTab,
  EnvioEquipamento,
  FormaPagamentoSaas,
  Inventario,
  LinkMonitorado,
  Pagina,
  Perfil,
  PlanoSaas,
  Prioridade,
  Sessao,
  UsuarioLista,
} from './models/uniflowit.models';

@Component({
  selector: 'app-root',
  imports: [CommonModule, FormsModule, NgxSpinnerModule, LoginPage, DashboardPage, EmpresasPage, UsuariosPage, CategoriasPage, ChamadosPage, ConhecimentoPage, EquipamentosPage, LinksPage, SaasAdminPage],
  templateUrl: './app.html',
  styleUrl: './app.scss',
  encapsulation: ViewEncapsulation.None,
})
export class App implements OnInit {
  private readonly apiUrl = 'http://localhost:5151/api';
  private readonly temaStorageKey = 'uniflowit-theme';

  protected readonly perfis: Perfil[] = ['Usuario', 'Atendente', 'Administrador', 'AdministradorSaas'];
  protected tema = signal<'dark' | 'light'>('dark');
  protected perfil = signal<Perfil>('Usuario');
  protected authMode = signal<AuthMode>('login');
  protected sessao = signal<Sessao | null>(null);
  protected paginaAtiva = signal<Pagina>('chamados');
  protected cadastroAberto = signal(true);
  protected comercialAberto = signal(false);
  protected operacaoSaasAberto = signal(false);
  protected plataformaSaasAberto = signal(false);
  protected administracaoSaasAberto = signal(false);
  protected empresaTab = signal<EmpresaTab>('pesquisa');
  protected empresaFiltro = signal('');
  protected empresaSelecionadaId = signal<number | null>(1);
  protected empresaEditando = signal(false);
  protected empresaModalAberto = signal(false);
  protected usuarioModalAberto = signal(false);
  protected usuarioEditando = signal(false);
  protected categoriaModalAberto = signal(false);
  protected contaMenuAberto = signal(false);
  protected perfilModalAberto = signal(false);
  protected senhaModalAberto = signal(false);
  protected existeAdministradorSaas = signal(true);
  protected authFeedback = signal('');
  protected carregandoAuth = signal(false);
  protected chamadoSelecionadoId = signal(1);
  protected novaMensagem = '';
  protected avaliacaoSelecionada = 5;

  protected loginForm = {
    email: '',
    senha: '',
  };

  protected adminSaasForm = {
    nome: '',
    email: '',
    senha: '',
  };

  protected perfilForm = {
    nome: '',
    email: '',
    fotoUrl: '',
  };

  protected senhaForm = {
    atual: '',
    nova: '',
    confirmacao: '',
  };

  protected novaEmpresa: EmpresaForm = {
    id: undefined as number | undefined,
    nome: '',
    razaoSocial: '',
    nomeFantasia: '',
    tenantSlug: '',
    cnpj: '',
    telefone: '',
    email: '',
    endereco: '',
    numero: '',
    complemento: '',
    bairro: '',
    cidade: '',
    estado: '',
    cep: '',
    inscricaoMunicipal: '',
    inscricaoEstadual: '',
    logoUrl: '',
    ativo: true,
    acessoBloqueado: false,
    motivoBloqueio: '',
    bloqueadoEm: '',
    dataCadastro: '',
  };

  protected novoUsuario = {
    id: undefined as number | undefined,
    empresaId: null as number | null,
    nome: '',
    email: '',
    senha: '',
    senhaConfirmacao: '',
    role: 'Usuario' as Perfil,
    ativo: true,
  };

  protected categoriaForm: CategoriaChamadoForm = {
    empresaId: 1,
    nome: '',
    subcategoriasTexto: '',
    prioridadePadrao: 'Media',
    ativo: true,
  };

  protected dadosEmpresaSaas: DadosEmpresaSaas = {
    razaoSocial: 'UniFlowIT Tecnologia LTDA',
    nomeFantasia: 'UniFlowIT',
    cnpj: '',
    inscricaoMunicipal: '',
    inscricaoEstadual: '',
    regimeTributario: 'Simples Nacional',
    emailFinanceiro: 'financeiro@uniflowit.com',
    telefone: '',
    endereco: '',
    numero: '',
    complemento: '',
    bairro: '',
    cidade: 'Sao Paulo',
    estado: 'SP',
    cep: '',
  };

  protected planos = signal<PlanoSaas[]>([
    { id: 1, nome: 'Starter', descricao: 'Operacao inicial de suporte e chamados.', limiteUsuarios: 25, limiteEmpresas: 1, valorMensal: 149.9, ativo: true },
    { id: 2, nome: 'Business', descricao: 'Gestao completa de TI para clientes em crescimento.', limiteUsuarios: 100, limiteEmpresas: 1, valorMensal: 399.9, ativo: true },
  ]);

  protected planoForm: PlanoSaas = { id: 0, nome: '', descricao: '', limiteUsuarios: 10, limiteEmpresas: 1, valorMensal: 0, ativo: true };

  protected assinaturas = signal<AssinaturaSaas[]>([
    { id: 1, empresaId: 1, planoId: 2, dataInicio: '2026-08-01', dataFim: '', status: 'Ativa', valorMensal: 399.9 },
  ]);

  protected assinaturaForm: AssinaturaSaas = { id: 0, empresaId: 1, planoId: 1, dataInicio: new Date().toISOString().slice(0, 10), dataFim: '', status: 'Ativa', valorMensal: 0 };

  protected cobrancas = signal<CobrancaSaas[]>([
    { id: 1, empresaId: 1, assinaturaId: 1, vencimento: '2026-08-10', valor: 399.9, status: 'Aberta', formaPagamento: 'Pix manual' },
  ]);

  protected cobrancaForm: CobrancaSaas = { id: 0, empresaId: 1, assinaturaId: 1, vencimento: new Date().toISOString().slice(0, 10), valor: 0, status: 'Aberta', formaPagamento: 'Pix manual' };

  protected formasPagamento = signal<FormaPagamentoSaas[]>([
    { id: 1, nome: 'Pix manual', tipo: 'Pix manual', chavePix: '', recebedorPix: 'UNIFLOWIT', cidadePix: 'SAO PAULO', mercadoPagoPublicKey: '', mercadoPagoAccessToken: '', ativo: true },
    { id: 2, nome: 'Mercado Pago', tipo: 'Mercado Pago', chavePix: '', recebedorPix: '', cidadePix: '', mercadoPagoPublicKey: '', mercadoPagoAccessToken: '', ativo: false },
  ]);

  protected formaPagamentoForm: FormaPagamentoSaas = { id: 0, nome: 'Pix manual', tipo: 'Pix manual', chavePix: '', recebedorPix: 'UNIFLOWIT', cidadePix: 'SAO PAULO', mercadoPagoPublicKey: '', mercadoPagoAccessToken: '', ativo: true };

  protected despesas = signal<DespesaSaas[]>([
    { id: 1, descricao: 'Hospedagem e infraestrutura', fornecedor: 'Cloud', categoria: 'Infraestrutura', vencimento: '2026-08-15', valor: 120, status: 'Aberta' },
  ]);

  protected despesaForm: DespesaSaas = { id: 0, descricao: '', fornecedor: '', categoria: 'Infraestrutura', vencimento: new Date().toISOString().slice(0, 10), valor: 0, status: 'Aberta' };

  protected empresas = signal<EmpresaLista[]>([
    {
      id: 1,
      nome: 'UniFlowIT Demo',
      razaoSocial: 'UniFlowIT Demo Tecnologia LTDA',
      nomeFantasia: 'UniFlowIT Demo',
      tenantSlug: 'uniflowit-demo',
      cnpj: '00.000.000/0001-00',
      telefone: '(11) 4000-0000',
      email: 'ti@demo.com',
      endereco: 'Avenida Paulista',
      numero: '1000',
      complemento: 'Conjunto 1201',
      bairro: 'Bela Vista',
      cidade: 'Sao Paulo',
      estado: 'SP',
      cep: '01310-100',
      inscricaoMunicipal: '',
      inscricaoEstadual: '',
      logoUrl: '',
      ativo: true,
      acessoBloqueado: false,
      motivoBloqueio: '',
      bloqueadoEm: '',
      dataCadastro: '2026-07-20',
    },
  ]);

  protected usuarios = signal<UsuarioLista[]>([]);

  protected categorias = signal<CategoriaChamado[]>([
    { id: 1, empresaId: 1, nome: 'Acesso', subcategorias: ['E-mail', 'Sistema interno', 'VPN'], prioridadePadrao: 'Media', ativo: true },
    { id: 2, empresaId: 1, nome: 'Equipamentos', subcategorias: ['Notebook', 'Desktop', 'Monitor', 'Perifericos'], prioridadePadrao: 'Baixa', ativo: true },
    { id: 3, empresaId: 1, nome: 'Infraestrutura', subcategorias: ['Link internet', 'Firewall', 'Servidor', 'Backup'], prioridadePadrao: 'Alta', ativo: true },
    { id: 4, empresaId: 1, nome: 'Sistemas', subcategorias: ['ERP', 'CRM', 'Portal interno'], prioridadePadrao: 'Media', ativo: true },
  ]);

  protected novoChamado = {
    titulo: '',
    solicitante: 'Eduardo Lima',
    categoria: 'Acesso',
    subcategoria: 'Sistema interno',
    tipo: 'Incidente' as Chamado['tipo'],
    prioridade: 'Media' as Prioridade,
    descricao: '',
    anexos: '',
  };

  protected chamados = signal<Chamado[]>([]);

  protected artigos: Artigo[] = [
    {
      empresaId: 1,
      titulo: 'Como abrir um chamado com evidencias',
      categoria: 'Central de chamados',
      resumo: 'Inclua prints, mensagem de erro, horario do problema e impacto para acelerar o atendimento.',
      tags: ['chamado', 'evidencia'],
    },
    {
      empresaId: 1,
      titulo: 'Primeiros passos quando a internet falhar',
      categoria: 'Infraestrutura',
      resumo: 'Confira cabo, Wi-Fi, VPN e reinicie o navegador antes de acionar o suporte.',
      tags: ['internet', 'link'],
    },
    {
      empresaId: 1,
      titulo: 'Politica de envio de equipamentos',
      categoria: 'Equipamentos',
      resumo: 'Todo envio para filial precisa de patrimonio, responsavel, termo e confirmacao de recebimento.',
      tags: ['patrimonio', 'filial'],
    },
  ];

  protected envios: EnvioEquipamento[] = [
    { empresaId: 1, patrimonio: 'UNI-NB-1042', tipo: 'Notebook', filialDestino: 'Campinas', responsavel: 'TI Matriz', status: 'Enviado' },
    { empresaId: 1, patrimonio: 'UNI-MON-2210', tipo: 'Monitor', filialDestino: 'Santos', responsavel: 'Almoxarifado', status: 'Preparando' },
  ];

  protected inventario: Inventario[] = [
    { empresaId: 1, patrimonio: 'UNI-NB-1042', hostname: 'NB-CPS-1042', usuario: 'ana.sales', filial: 'Campinas', sistema: 'Windows 11 Pro', memoria: '16 GB' },
    { empresaId: 1, patrimonio: 'UNI-SRV-002', hostname: 'SRV-AD-002', usuario: 'Sistema', filial: 'Matriz', sistema: 'Windows Server', memoria: '64 GB' },
  ];

  protected links = signal<LinkMonitorado[]>([
    { empresaId: 1, nome: 'Matriz - Internet principal', firewall: 'Fortigate Matriz', endereco: '200.10.10.1', intervalo: 30, disponivel: true },
    { empresaId: 1, nome: 'Campinas - MPLS', firewall: 'Fortigate Campinas', endereco: '10.255.20.1', intervalo: 60, disponivel: false, chamado: 'CH-20260720-0002' },
  ]);

  constructor(
    private readonly spinner: NgxSpinnerService,
    private readonly toastr: ToastrService,
  ) {}

  @HostBinding('class.theme-dark')
  protected get temaEscuro(): boolean {
    return this.tema() === 'dark';
  }

  @HostBinding('class.theme-light')
  protected get temaClaro(): boolean {
    return this.tema() === 'light';
  }

  async ngOnInit(): Promise<void> {
    this.carregarTema();

    const empresaInicial = this.empresaSelecionada();
    if (empresaInicial) {
      this.novaEmpresa = { ...empresaInicial, nome: empresaInicial.nome ?? empresaInicial.nomeFantasia };
    }

    await this.verificarAdministradorSaas();
  }

  protected alternarTema(): void {
    const proximoTema = this.tema() === 'dark' ? 'light' : 'dark';
    this.tema.set(proximoTema);
    localStorage.setItem(this.temaStorageKey, proximoTema);
  }

  private carregarTema(): void {
    const temaSalvo = localStorage.getItem(this.temaStorageKey);
    this.tema.set(temaSalvo === 'light' ? 'light' : 'dark');
  }

  protected chamadosVisiveis = computed(() => {
    if (this.perfil() === 'AdministradorSaas') {
      return this.chamados();
    }

    const empresaId = this.sessao()?.empresaId;
    const chamadosEmpresa = this.chamados()
      .filter((chamado) => chamado.origem !== 'Desenvolvedor')
      .filter((chamado) => !empresaId || chamado.empresaId === empresaId || chamado.empresaId == null);

    if (this.perfil() === 'Administrador') {
      return chamadosEmpresa;
    }

    if (this.perfil() === 'Atendente') {
      return chamadosEmpresa.filter((chamado) => chamado.status === 'Aberto' || chamado.atendente === this.sessao()?.nome);
    }

    return chamadosEmpresa.filter(
      (chamado) =>
        chamado.status === 'Aberto' &&
        (chamado.solicitanteUsuarioId === this.sessao()?.id || chamado.solicitante === this.sessao()?.nome),
    );
  });

  protected chamadosDesenvolvedorVisiveis = computed(() => {
    const chamados = this.chamados().filter((chamado) => chamado.origem === 'Desenvolvedor');
    if (this.perfil() === 'AdministradorSaas') {
      return chamados;
    }

    const empresaId = this.sessao()?.empresaId;
    return chamados.filter((chamado) => !empresaId || chamado.empresaId === empresaId);
  });

  protected categoriasDesenvolvedor = computed<CategoriaChamado[]>(() => [
    {
      nome: 'Plataforma UniFlowIT',
      subcategorias: ['Erro do sistema', 'Ajuste de funcionalidade', 'Melhoria', 'Integracao'],
      prioridadePadrao: 'Media',
      ativo: true,
    },
  ]);

  protected chamadoSelecionado = computed(() => {
    return this.chamados().find((chamado) => chamado.id === this.chamadoSelecionadoId()) ?? this.chamados()[0];
  });

  protected metricas = computed(() => {
    const chamados = this.chamadosVisiveis();
    return {
      abertos: chamados.filter((item) => item.status === 'Aberto').length,
      atendimento: chamados.filter((item) => item.status === 'Em atendimento').length,
      urgentes: chamados.filter((item) => item.prioridade === 'Urgente').length,
      linksFora: this.links().filter((item) => !item.disponivel).length,
    };
  });

  protected categoriasVisiveis = computed(() => {
    const empresaId = this.sessao()?.empresaId;
    return this.categorias().filter((categoria) => !empresaId || categoria.empresaId === empresaId || categoria.empresaId == null);
  });

  protected categoriasAtivas = computed(() => {
    return this.categoriasVisiveis().filter((categoria) => categoria.ativo);
  });

  protected tituloPagina = computed(() => {
    switch (this.paginaAtiva()) {
      case 'dashboard':
        return 'Dashboard da empresa';
      case 'chamados-desenvolvedor':
        return 'Chamados para o desenvolvedor';
      case 'saas-dashboard':
        return 'Dashboard SaaS';
      case 'cadastro-empresas':
        return 'Empresas contratantes';
      case 'cadastro-usuarios':
        return 'Usuarios por empresa';
      case 'dados-empresariais':
        return 'Dados empresariais';
      case 'financeiro-assinaturas':
        return 'Assinaturas';
      case 'financeiro-cobrancas':
        return 'Cobrancas';
      case 'financeiro-planos':
        return 'Planos';
      case 'financeiro-pagamentos':
        return 'Formas de pagamento';
      case 'financeiro-despesas':
        return 'Despesas';
      case 'saas-implantacoes':
        return 'Implantacoes';
      case 'saas-inadimplencia':
        return 'Inadimplencia';
      case 'saas-chamados-globais':
        return 'Chamados globais';
      case 'saas-sla-plataforma':
        return 'SLA da plataforma';
      case 'saas-incidentes':
        return 'Incidentes';
      case 'saas-monitoramento':
        return 'Monitoramento SaaS';
      case 'saas-agentes':
        return 'Agentes';
      case 'saas-integracoes':
        return 'Integracoes';
      case 'saas-acesso-remoto':
        return 'Acesso remoto';
      case 'saas-relatorios':
        return 'Relatorios SaaS';
      case 'saas-metricas-uso':
        return 'Metricas de uso';
      case 'saas-auditoria':
        return 'Auditoria';
      case 'saas-configuracoes':
        return 'Configuracoes SaaS';
      case 'saas-seguranca':
        return 'Seguranca';
      case 'saas-administradores':
        return 'Administradores SaaS';
      case 'cadastro-categorias':
        return 'Categorias de chamados';
      case 'conhecimento':
        return 'Base de conhecimento';
      case 'equipamentos':
        return 'Equipamentos';
      case 'links':
        return 'Monitoramento de links';
      default:
        return 'Central de chamados';
    }
  });

  protected contextoPagina = computed(() => {
    return this.perfil() === 'AdministradorSaas' ? 'Administracao SaaS' : this.sessao()?.empresaNome || 'Fase 1';
  });

  protected paginaSaasConteudo(): boolean {
    const pagina = this.paginaAtiva();
    return this.perfil() === 'AdministradorSaas' && pagina !== 'cadastro-empresas' && pagina !== 'cadastro-usuarios';
  }

  protected empresasFiltradas = computed(() => {
    const filtro = this.empresaFiltro().trim().toLowerCase();
    if (!filtro) {
      return this.empresas();
    }

    return this.empresas().filter((empresa) =>
      [empresa.razaoSocial, empresa.nomeFantasia, empresa.cnpj, empresa.email, empresa.telefone]
        .some((valor) => valor.toLowerCase().includes(filtro)),
    );
  });

  protected empresaSelecionada = computed(() => {
    return this.empresas().find((empresa) => empresa.id === this.empresaSelecionadaId()) ?? this.empresas()[0];
  });

  protected async login(): Promise<void> {
    this.carregandoAuth.set(true);
    this.authFeedback.set('');
    this.spinner.show('uniflowit');

    try {
      const response = await fetch(`${this.apiUrl}/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(this.loginForm),
      });

      if (!response.ok) {
        this.authFeedback.set('E-mail ou senha invalidos.');
        this.toastr.error('E-mail ou senha invalidos.', 'Acesso negado');
        return;
      }

      const data = (await response.json()) as Sessao;
      this.sessao.set(data);
      this.perfil.set(data.role);
      this.paginaAtiva.set(data.role === 'AdministradorSaas' ? 'saas-dashboard' : data.role === 'Administrador' ? 'dashboard' : 'chamados');
      this.cadastroAberto.set(data.role === 'AdministradorSaas' || data.role === 'Administrador');
      if (data.empresaId) {
        this.novoUsuario.empresaId = data.empresaId;
        this.empresaSelecionadaId.set(data.empresaId);
      }
      this.novoChamado.solicitante = data.nome;
      await this.carregarDadosSistema();
      this.toastr.success(`Bem-vindo, ${data.nome}.`, 'Login realizado');
    } catch {
      this.authFeedback.set('Nao foi possivel conectar na API. Verifique se ela esta rodando em localhost:5151.');
      this.toastr.error('Nao foi possivel conectar na API.', 'Falha de conexao');
    } finally {
      this.carregandoAuth.set(false);
      this.spinner.hide('uniflowit');
    }
  }

  protected async criarAdministradorSaas(): Promise<void> {
    if (!this.senhaForteValida(this.adminSaasForm.senha)) {
      this.authFeedback.set(this.mensagemSenhaForte());
      this.toastr.warning(this.mensagemSenhaForte(), 'Senha fraca');
      return;
    }

    this.carregandoAuth.set(true);
    this.authFeedback.set('');
    this.spinner.show('uniflowit');

    try {
      const response = await fetch(`${this.apiUrl}/auth/criar-administrador-saas`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(this.adminSaasForm),
      });

      if (!response.ok) {
        this.authFeedback.set('Nao foi possivel criar o Administrador SaaS. Talvez ele ja exista.');
        this.toastr.error('Administrador SaaS ja existe ou dados invalidos.', 'Nao foi possivel criar');
        return;
      }

      const data = (await response.json()) as Sessao;
      this.sessao.set(data);
      this.perfil.set('AdministradorSaas');
      this.paginaAtiva.set('saas-dashboard');
      this.cadastroAberto.set(true);
      this.existeAdministradorSaas.set(true);
      this.loginForm.email = data.email;
      this.authFeedback.set('Administrador SaaS criado com sucesso.');
      this.toastr.success('Administrador SaaS criado com sucesso.', 'Primeiro acesso');
    } catch {
      this.authFeedback.set('Nao foi possivel conectar na API para criar o Administrador SaaS.');
      this.toastr.error('Nao foi possivel conectar na API.', 'Falha de conexao');
    } finally {
      this.carregandoAuth.set(false);
      this.spinner.hide('uniflowit');
    }
  }

  protected async criarEmpresa(): Promise<void> {
    this.spinner.show('uniflowit');
    const dataCadastro = this.novaEmpresa.dataCadastro || new Date().toISOString().slice(0, 10);
    const nomeBase = this.novaEmpresa.nomeFantasia || this.novaEmpresa.razaoSocial || this.novaEmpresa.nome || 'empresa';
    const tenantSlug = this.novaEmpresa.tenantSlug || this.gerarTenantSlug(nomeBase);
    const cnpj = this.formatarCnpj(this.novaEmpresa.cnpj);

    const telefone = this.formatarTelefone(this.novaEmpresa.telefone);
    const ativo = this.novaEmpresa.id ? this.novaEmpresa.ativo : true;
    const empresa: EmpresaLista = {
      id: this.novaEmpresa.id,
      nome: this.novaEmpresa.nome || this.novaEmpresa.nomeFantasia || this.novaEmpresa.razaoSocial,
      razaoSocial: this.novaEmpresa.razaoSocial,
      nomeFantasia: this.novaEmpresa.nomeFantasia,
      tenantSlug,
      cnpj,
      telefone,
      email: this.novaEmpresa.email.trim().toLowerCase(),
      endereco: this.novaEmpresa.endereco,
      numero: this.novaEmpresa.numero,
      complemento: this.novaEmpresa.complemento,
      bairro: this.novaEmpresa.bairro,
      cidade: this.novaEmpresa.cidade,
      estado: this.novaEmpresa.estado,
      cep: this.novaEmpresa.cep,
      inscricaoMunicipal: this.novaEmpresa.inscricaoMunicipal,
      inscricaoEstadual: this.novaEmpresa.inscricaoEstadual,
      logoUrl: this.novaEmpresa.logoUrl,
      ativo,
      acessoBloqueado: this.novaEmpresa.id ? !ativo : false,
      motivoBloqueio: ativo ? '' : this.novaEmpresa.motivoBloqueio || 'Empresa inativada pelo Administrador SaaS.',
      bloqueadoEm: ativo ? '' : this.novaEmpresa.bloqueadoEm || new Date().toISOString(),
      dataCadastro,
    };

    let empresaPersistida: EmpresaLista | null = null;

    try {
      const response = await fetch(`${this.apiUrl}/empresas${empresa.id ? `/${empresa.id}` : ''}`, {
        method: empresa.id ? 'PUT' : 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(empresa),
      });

      if (!response.ok) {
        const mensagem = await this.lerMensagemErro(response);
        throw new Error(mensagem || 'Nao foi possivel salvar a empresa.');
      }

      empresaPersistida = this.mapearEmpresa((await response.json()) as Record<string, unknown>);
      this.empresas.update((empresas) => {
        const existe = empresas.some((item) => item.id === empresaPersistida?.id);
        return existe ? empresas.map((item) => (item.id === empresaPersistida?.id ? empresaPersistida! : item)) : [empresaPersistida!, ...empresas];
      });

      this.empresaSelecionadaId.set(empresaPersistida.id ?? null);
      this.empresaEditando.set(false);
      this.empresaTab.set('detalhes');
      this.novoUsuario.empresaId = empresaPersistida.id ?? this.novoUsuario.empresaId;
      this.toastr.success('Empresa salva com sucesso.', 'Cadastro');
    } catch (error) {
      const mensagem = error instanceof Error ? error.message : 'API indisponivel ou recusou a persistencia.';
      this.authFeedback.set(mensagem);
      this.toastr.error(mensagem, 'Empresa nao salva');
    } finally {
      if (empresaPersistida) {
        this.empresaModalAberto.set(false);
        this.empresaEditando.set(false);
      }
      this.spinner.hide('uniflowit');
    }

    if (empresaPersistida) {
      this.novaEmpresa = { ...empresaPersistida, nome: empresaPersistida.nome ?? empresaPersistida.nomeFantasia };
    }
  }

  protected async criarUsuario(): Promise<void> {
    this.spinner.show('uniflowit');
    const empresaId = this.empresaPermitidaParaUsuario();
    this.novoUsuario.empresaId = empresaId;
    if (this.perfil() === 'AdministradorSaas') {
      this.novoUsuario.role = 'Administrador';
    }
    const editando = this.usuarioEditando();
    const id = this.novoUsuario.id;

    if (!empresaId) {
      this.toastr.warning('Selecione a empresa contratante.', 'Cadastro');
      this.spinner.hide('uniflowit');
      return;
    }

    if (!this.novoUsuario.nome.trim()) {
      this.toastr.warning('Informe o nome do usuario.', 'Cadastro');
      this.spinner.hide('uniflowit');
      return;
    }

    const senhaFoiInformada = Boolean(this.novoUsuario.senha || this.novoUsuario.senhaConfirmacao);

    if ((!editando || senhaFoiInformada) && this.novoUsuario.senha !== this.novoUsuario.senhaConfirmacao) {
      this.toastr.warning('A senha e a confirmacao devem ser iguais.', 'Senha divergente');
      this.spinner.hide('uniflowit');
      return;
    }

    if ((!editando || senhaFoiInformada) && !this.senhaForteValida(this.novoUsuario.senha)) {
      this.toastr.warning(this.mensagemSenhaForte(), 'Senha fraca');
      this.spinner.hide('uniflowit');
      return;
    }

    const email = this.novoUsuario.email.trim().toLowerCase();
    if (!this.emailValido(email)) {
      this.toastr.warning('Informe um e-mail valido.', 'Cadastro');
      this.spinner.hide('uniflowit');
      return;
    }

    let salvou = false;

    try {
      const response = await fetch(`${this.apiUrl}/usuarios${editando ? `/${id}` : ''}`, {
        method: editando ? 'PUT' : 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          empresaId,
          nome: this.novoUsuario.nome.trim(),
          email,
          senha: this.novoUsuario.senha,
          role: this.novoUsuario.role,
          ativo: this.novoUsuario.ativo,
        }),
      });

      if (!response.ok) {
        const mensagem = await this.lerMensagemErro(response);
        throw new Error(mensagem || 'Falha ao salvar usuario');
      }

      const usuarioSalvo = (await response.json()) as Record<string, unknown>;
      const usuarioPersistido: UsuarioLista = {
        id: Number(usuarioSalvo['id'] ?? id ?? 0),
        empresaId: usuarioSalvo['empresaId'] == null ? undefined : Number(usuarioSalvo['empresaId']),
        empresaNome: String(usuarioSalvo['empresaNome'] ?? this.empresas().find((item) => item.id === empresaId)?.nome ?? 'Empresa nao localizada'),
        nome: String(usuarioSalvo['nome'] ?? this.novoUsuario.nome.trim()),
        email: String(usuarioSalvo['email'] ?? email),
        role: this.normalizarPerfil(usuarioSalvo['role']),
        ativo: Boolean(usuarioSalvo['ativo']),
      };

      this.usuarios.update((usuarios) => editando
        ? usuarios.map((item) => (item.id === usuarioPersistido.id ? usuarioPersistido : item))
        : [usuarioPersistido, ...usuarios]);

      this.toastr.success(editando ? 'Usuario atualizado com sucesso.' : 'Usuario criado com sucesso.', 'Cadastro');
      salvou = true;
    } catch (error) {
      const mensagem = error instanceof Error ? error.message : 'API indisponivel ou recusou a persistencia.';
      this.authFeedback.set(mensagem);
      this.toastr.error(mensagem, 'Usuario nao salvo');
    } finally {
      if (salvou) {
        this.usuarioModalAberto.set(false);
        this.usuarioEditando.set(false);
      }
      this.spinner.hide('uniflowit');
    }

    if (salvou) {
      this.novoUsuario = {
        id: undefined,
        empresaId: this.perfil() === 'AdministradorSaas' ? null : this.empresaPermitidaParaUsuario(),
        nome: '',
        email: '',
        senha: '',
        senhaConfirmacao: '',
        role: this.perfil() === 'AdministradorSaas' ? 'Administrador' : 'Usuario',
        ativo: true
      };
    }
  }

  protected sair(): void {
    this.sessao.set(null);
    this.authFeedback.set('');
    this.perfil.set('Usuario');
    this.paginaAtiva.set('chamados');
    this.cadastroAberto.set(true);
    this.contaMenuAberto.set(false);
  }

  protected alternarMenuConta(): void {
    this.contaMenuAberto.update((aberto) => !aberto);
  }

  protected abrirEditarPerfil(): void {
    const sessao = this.sessao();
    this.perfilForm = {
      nome: sessao?.nome ?? '',
      email: sessao?.email ?? '',
      fotoUrl: this.fotoPerfilUrl(),
    };
    this.contaMenuAberto.set(false);
    this.perfilModalAberto.set(true);
  }

  protected fecharEditarPerfil(): void {
    this.perfilModalAberto.set(false);
  }

  protected salvarPerfil(): void {
    const sessao = this.sessao();
    if (!sessao) {
      return;
    }

    this.sessao.set({
      ...sessao,
      nome: this.perfilForm.nome.trim() || sessao.nome,
      email: this.perfilForm.email.trim() || sessao.email,
    });

    localStorage.setItem(`uniflowit:fotoperfil:${sessao.id}`, this.perfilForm.fotoUrl.trim());
    this.novoChamado.solicitante = this.sessao()?.nome ?? this.novoChamado.solicitante;
    this.perfilModalAberto.set(false);
    this.toastr.success('Perfil atualizado com sucesso.', 'Minha conta');
  }

  protected abrirAlterarSenha(): void {
    this.senhaForm = { atual: '', nova: '', confirmacao: '' };
    this.contaMenuAberto.set(false);
    this.senhaModalAberto.set(true);
  }

  protected fecharAlterarSenha(): void {
    this.senhaModalAberto.set(false);
  }

  protected async alterarSenha(): Promise<void> {
    if (!this.senhaForm.nova || this.senhaForm.nova !== this.senhaForm.confirmacao) {
      this.toastr.warning('Confirme a nova senha corretamente.', 'Minha conta');
      return;
    }

    if (!this.senhaForteValida(this.senhaForm.nova)) {
      this.toastr.warning(this.mensagemSenhaForte(), 'Senha fraca');
      return;
    }

    const sessao = this.sessao();
    if (!sessao) {
      return;
    }

    this.spinner.show('uniflowit');
    try {
      const response = await fetch(`${this.apiUrl}/usuarios/${sessao.id}/senha`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          senhaAtual: this.senhaForm.atual,
          novaSenha: this.senhaForm.nova,
        }),
      });

      if (!response.ok) {
        this.toastr.error(response.status === 401 ? 'Senha atual incorreta.' : 'Nao foi possivel alterar a senha.', 'Minha conta');
        return;
      }

      this.senhaModalAberto.set(false);
      this.toastr.success('Senha alterada com sucesso.', 'Minha conta');
    } catch {
      this.toastr.error('Nao foi possivel conectar na API para alterar a senha.', 'Falha de conexao');
    } finally {
      this.spinner.hide('uniflowit');
    }
  }

  protected iniciaisUsuario(): string {
    const nome = this.sessao()?.nome ?? 'UF';
    return nome
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map((parte) => parte[0]?.toUpperCase())
      .join('') || 'UF';
  }

  protected fotoPerfilUrl(): string {
    const id = this.sessao()?.id;
    return id ? localStorage.getItem(`uniflowit:fotoperfil:${id}`) ?? '' : '';
  }

  protected alterarModoAuth(modo: AuthMode): void {
    this.authMode.set(modo);
    if (modo === 'login') {
      void this.verificarAdministradorSaas();
    }
  }

  protected alterarEmpresaTab(tab: EmpresaTab): void {
    if (tab === 'detalhes' && !this.empresaFiltro().trim()) {
      this.empresaTab.set('pesquisa');
      this.toastr.info('Pesquise uma empresa antes de abrir os dados cadastrais.', 'Pesquisa obrigatoria');
      return;
    }

    this.empresaTab.set(tab);
  }

  protected navegar(pagina: Pagina): void {
    if (this.perfil() === 'Usuario' || this.perfil() === 'Atendente') {
      this.paginaAtiva.set(pagina === 'chamados-desenvolvedor' ? 'chamados-desenvolvedor' : 'chamados');
      return;
    }

    const paginasSaas: Pagina[] = [
      'cadastro-empresas',
      'saas-dashboard',
      'chamados-desenvolvedor',
      'cadastro-usuarios',
      'dados-empresariais',
      'financeiro-assinaturas',
      'financeiro-cobrancas',
      'financeiro-planos',
      'financeiro-pagamentos',
      'financeiro-despesas',
      'saas-implantacoes',
      'saas-inadimplencia',
      'saas-chamados-globais',
      'saas-sla-plataforma',
      'saas-incidentes',
      'saas-monitoramento',
      'saas-agentes',
      'saas-integracoes',
      'saas-acesso-remoto',
      'saas-relatorios',
      'saas-metricas-uso',
      'saas-auditoria',
      'saas-configuracoes',
      'saas-seguranca',
      'saas-administradores',
    ];

    if (this.perfil() === 'AdministradorSaas' && !paginasSaas.includes(pagina)) {
      this.paginaAtiva.set('saas-dashboard');
      return;
    }

    this.paginaAtiva.set(pagina);
  }

  protected salvarDadosEmpresaSaas(): void {
    this.toastr.success('Dados empresariais salvos para emissao fiscal.', 'Administrador SaaS');
  }

  protected salvarPlanoSaas(): void {
    const id = this.planoForm.id || this.proximoId(this.planos());
    const plano = { ...this.planoForm, id };
    this.planos.update((planos) => this.salvarNaLista(planos, plano));
    this.planoForm = { id: 0, nome: '', descricao: '', limiteUsuarios: 10, limiteEmpresas: 1, valorMensal: 0, ativo: true };
    this.toastr.success('Plano salvo com sucesso.', 'Financeiro');
  }

  protected salvarAssinaturaSaas(): void {
    const id = this.assinaturaForm.id || this.proximoId(this.assinaturas());
    const assinatura = { ...this.assinaturaForm, id };
    this.assinaturas.update((assinaturas) => this.salvarNaLista(assinaturas, assinatura));
    this.assinaturaForm = { id: 0, empresaId: this.empresas()[0]?.id ?? 1, planoId: this.planos()[0]?.id ?? 1, dataInicio: new Date().toISOString().slice(0, 10), dataFim: '', status: 'Ativa', valorMensal: 0 };
    this.toastr.success('Assinatura salva com sucesso.', 'Financeiro');
  }

  protected salvarCobrancaSaas(): void {
    const id = this.cobrancaForm.id || this.proximoId(this.cobrancas());
    const cobranca = { ...this.cobrancaForm, id };
    this.cobrancas.update((cobrancas) => this.salvarNaLista(cobrancas, cobranca));
    this.cobrancaForm = { id: 0, empresaId: this.empresas()[0]?.id ?? 1, assinaturaId: this.assinaturas()[0]?.id ?? 1, vencimento: new Date().toISOString().slice(0, 10), valor: 0, status: 'Aberta', formaPagamento: this.formasPagamento()[0]?.nome ?? 'Pix manual' };
    this.toastr.success('Cobranca salva com sucesso.', 'Financeiro');
  }

  protected salvarFormaPagamentoSaas(): void {
    const id = this.formaPagamentoForm.id || this.proximoId(this.formasPagamento());
    const forma = { ...this.formaPagamentoForm, id };
    this.formasPagamento.update((formas) => this.salvarNaLista(formas, forma));
    this.formaPagamentoForm = { id: 0, nome: 'Pix manual', tipo: 'Pix manual', chavePix: '', recebedorPix: this.dadosEmpresaSaas.nomeFantasia || 'UNIFLOWIT', cidadePix: this.dadosEmpresaSaas.cidade || 'SAO PAULO', mercadoPagoPublicKey: '', mercadoPagoAccessToken: '', ativo: true };
    this.toastr.success('Forma de pagamento salva com sucesso.', 'Financeiro');
  }

  protected salvarDespesaSaas(): void {
    const id = this.despesaForm.id || this.proximoId(this.despesas());
    const despesa = { ...this.despesaForm, id };
    this.despesas.update((despesas) => this.salvarNaLista(despesas, despesa));
    this.despesaForm = { id: 0, descricao: '', fornecedor: '', categoria: 'Infraestrutura', vencimento: new Date().toISOString().slice(0, 10), valor: 0, status: 'Aberta' };
    this.toastr.success('Despesa salva com sucesso.', 'Financeiro');
  }

  private salvarNaLista<T extends { id: number }>(lista: T[], item: T): T[] {
    return lista.some((atual) => atual.id === item.id) ? lista.map((atual) => (atual.id === item.id ? item : atual)) : [item, ...lista];
  }

  private proximoId(lista: Array<{ id: number }>): number {
    return Math.max(0, ...lista.map((item) => item.id)) + 1;
  }

  private gerarTenantSlug(valor: string): string {
    const slug = valor
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-+|-+$/g, '');

    return slug || `empresa-${Date.now()}`;
  }

  private formatarCnpj(valor: string): string {
    return valor.replace(/[^a-zA-Z0-9]/g, '').toUpperCase().slice(0, 18);
  }

  private formatarTelefone(valor: string): string {
    return valor.replace(/\D/g, '').slice(0, 13);
  }

  private validarCnpj(valor: string): boolean {
    const cnpj = valor.replace(/\D/g, '');
    if (cnpj.length !== 14 || /^(\d)\1+$/.test(cnpj)) {
      return false;
    }

    const calcularDigito = (tamanho: number): number => {
      const pesos = tamanho === 12
        ? [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]
        : [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
      const soma = cnpj
        .slice(0, tamanho)
        .split('')
        .reduce((total, digito, index) => total + Number(digito) * pesos[index], 0);
      const resto = soma % 11;
      return resto < 2 ? 0 : 11 - resto;
    };

    return calcularDigito(12) === Number(cnpj[12]) && calcularDigito(13) === Number(cnpj[13]);
  }

  protected abrirNovaEmpresa(): void {
    this.resetarFormularioEmpresa();
    this.empresaSelecionadaId.set(null);
    this.empresaEditando.set(true);
    this.empresaModalAberto.set(true);
  }

  protected selecionarEmpresa(empresa: EmpresaLista): void {
    this.empresaSelecionadaId.set(empresa.id ?? null);
    this.novaEmpresa = { ...empresa, nome: empresa.nome ?? empresa.nomeFantasia };
    this.empresaEditando.set(false);
    this.empresaTab.set('detalhes');
  }

  protected editarEmpresa(): void {
    const empresa = this.empresaSelecionada();
    if (!empresa) {
      return;
    }

    this.novaEmpresa = { ...empresa, nome: empresa.nome ?? empresa.nomeFantasia };
    this.empresaEditando.set(true);
    this.empresaModalAberto.set(false);
  }

  protected async alternarEmpresaAtiva(): Promise<void> {
    const empresa = this.empresaSelecionada();
    if (!empresa) {
      return;
    }

    const ativa = !empresa.ativo;
    this.novaEmpresa = {
      ...empresa,
      nome: empresa.nome ?? empresa.nomeFantasia,
      ativo: ativa,
      acessoBloqueado: !ativa,
      motivoBloqueio: ativa ? '' : 'Empresa inativada pelo Administrador SaaS.',
      bloqueadoEm: ativa ? '' : new Date().toISOString(),
    };

    await this.criarEmpresa();
    this.toastr.info(ativa ? 'Empresa ativada e acesso liberado.' : 'Empresa inativada e acesso bloqueado.', 'Empresas');
  }

  protected fecharModalEmpresa(): void {
    this.empresaModalAberto.set(false);
    this.empresaEditando.set(false);
  }

  protected abrirNovoUsuario(): void {
    this.usuarioEditando.set(false);
    this.novoUsuario = {
      id: undefined,
      empresaId: this.perfil() === 'AdministradorSaas' ? null : this.empresaPermitidaParaUsuario(),
      nome: '',
      email: '',
      senha: '',
      senhaConfirmacao: '',
      role: this.perfil() === 'AdministradorSaas' ? 'Administrador' : 'Usuario',
      ativo: true
    };
    this.usuarioModalAberto.set(true);
  }

  protected editarUsuario(usuario: UsuarioLista): void {
    this.usuarioEditando.set(true);
    this.novoUsuario = {
      id: usuario.id,
      empresaId: usuario.empresaId ?? this.empresaPermitidaParaUsuario(),
      nome: usuario.nome,
      email: usuario.email,
      senha: '',
      senhaConfirmacao: '',
      role: usuario.role,
      ativo: usuario.ativo,
    };
    this.usuarioModalAberto.set(true);
  }

  protected fecharModalUsuario(): void {
    this.usuarioModalAberto.set(false);
    this.usuarioEditando.set(false);
  }

  protected gerarSenhaUsuario(): void {
    const senha = this.gerarSenhaForte();
    this.novoUsuario.senha = senha;
    this.novoUsuario.senhaConfirmacao = senha;
  }

  protected abrirNovaCategoria(): void {
    this.categoriaForm = {
      empresaId: this.sessao()?.empresaId,
      nome: '',
      subcategoriasTexto: '',
      prioridadePadrao: 'Media',
      ativo: true,
    };
    this.categoriaModalAberto.set(true);
  }

  protected editarCategoria(categoria: CategoriaChamado): void {
    this.categoriaForm = {
      id: categoria.id,
      empresaId: categoria.empresaId,
      nome: categoria.nome,
      subcategoriasTexto: categoria.subcategorias.join(', '),
      prioridadePadrao: categoria.prioridadePadrao,
      ativo: categoria.ativo,
    };
    this.categoriaModalAberto.set(true);
  }

  protected fecharModalCategoria(): void {
    this.categoriaModalAberto.set(false);
  }

  protected async salvarCategoria(): Promise<void> {
    const nome = this.categoriaForm.nome.trim();
    if (!nome) {
      this.toastr.warning('Informe o nome da categoria.', 'Cadastro');
      return;
    }

    const subcategorias = this.categoriaForm.subcategoriasTexto
      .split(',')
      .map((item) => item.trim())
      .filter(Boolean);

    const id = this.categoriaForm.id ?? Math.max(0, ...this.categorias().map((categoria) => categoria.id ?? 0)) + 1;
    const categoria: CategoriaChamado = {
      id,
      empresaId: this.categoriaForm.empresaId ?? this.sessao()?.empresaId,
      nome,
      subcategorias,
      prioridadePadrao: this.categoriaForm.prioridadePadrao,
      ativo: this.categoriaForm.ativo,
    };

    this.categorias.update((categorias) => {
      const existe = categorias.some((item) => item.id === id);
      return existe ? categorias.map((item) => (item.id === id ? categoria : item)) : [categoria, ...categorias];
    });

    if (!this.categoriasAtivas().some((item) => item.nome === this.novoChamado.categoria)) {
      this.novoChamado.categoria = categoria.nome;
      this.novoChamado.subcategoria = categoria.subcategorias[0] ?? '';
      this.novoChamado.prioridade = categoria.prioridadePadrao;
    }

    this.categoriaModalAberto.set(false);
    this.spinner.show('uniflowit');

    try {
      await fetch(`${this.apiUrl}/categorias-chamado${this.categoriaForm.id ? `/${this.categoriaForm.id}` : ''}`, {
        method: this.categoriaForm.id ? 'PUT' : 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          id: categoria.id,
          empresaId: categoria.empresaId,
          nome: categoria.nome,
          subcategorias: categoria.subcategorias.join(', '),
          prioridadePadrao: categoria.prioridadePadrao,
          ativo: categoria.ativo,
        }),
      });
      this.toastr.success('Categoria salva com sucesso.', 'Cadastro');
    } catch {
      this.toastr.info('Categoria salva localmente. API indisponivel para persistir agora.', 'Modo local');
    } finally {
      this.spinner.hide('uniflowit');
    }
  }

  protected alternarCadastro(): void {
    this.cadastroAberto.update((aberto) => !aberto);
  }

  protected alternarComercial(): void {
    this.comercialAberto.update((aberto) => !aberto);
  }

  protected alternarOperacaoSaas(): void {
    this.operacaoSaasAberto.update((aberto) => !aberto);
  }

  protected alternarPlataformaSaas(): void {
    this.plataformaSaasAberto.update((aberto) => !aberto);
  }

  protected alternarAdministracaoSaas(): void {
    this.administracaoSaasAberto.update((aberto) => !aberto);
  }

  protected empresasParaCadastroUsuario(): EmpresaLista[] {
    if (this.perfil() === 'AdministradorSaas') {
      return this.empresas();
    }

    const empresaId = this.sessao()?.empresaId;
    return this.empresas().filter((empresa) => empresa.id === empresaId);
  }

  protected usuariosVisiveisCadastro(): UsuarioLista[] {
    if (this.perfil() === 'AdministradorSaas') {
      return this.usuarios();
    }

    const empresaId = this.sessao()?.empresaId;
    return this.usuarios().filter((usuario) => usuario.empresaId === empresaId);
  }

  protected empresaDashboardNome(): string {
    return this.sessao()?.empresaNome || this.empresaSelecionada().nomeFantasia || this.empresaSelecionada().razaoSocial || 'Empresa nao identificada';
  }

  private senhaForteValida(senha: string): boolean {
    return senha.length >= 8
      && /[A-Z]/.test(senha)
      && /[0-9]/.test(senha)
      && /[A-Za-z]/.test(senha)
      && /[^A-Za-z0-9]/.test(senha);
  }

  private mensagemSenhaForte(): string {
    return 'A senha deve ter no minimo 8 caracteres, letra maiuscula, numero e caractere especial.';
  }

  private emailValido(email: string): boolean {
    return /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(email);
  }

  private gerarSenhaForte(): string {
    const maiusculas = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
    const minusculas = 'abcdefghijkmnopqrstuvwxyz';
    const numeros = '23456789';
    const especiais = '@#$%&*!?';
    const todos = `${maiusculas}${minusculas}${numeros}${especiais}`;
    const senha = [
      this.caractereAleatorio(maiusculas),
      this.caractereAleatorio(minusculas),
      this.caractereAleatorio(numeros),
      this.caractereAleatorio(especiais),
      ...Array.from({ length: 8 }, () => this.caractereAleatorio(todos)),
    ];

    return senha
      .map((caractere) => ({ caractere, ordem: crypto.getRandomValues(new Uint32Array(1))[0] }))
      .sort((a, b) => a.ordem - b.ordem)
      .map((item) => item.caractere)
      .join('');
  }

  private caractereAleatorio(opcoes: string): string {
    const valores = crypto.getRandomValues(new Uint32Array(1));
    return opcoes[valores[0] % opcoes.length];
  }

  private empresaPermitidaParaUsuario(): number | null {
    if (this.perfil() === 'AdministradorSaas') {
      return this.novoUsuario.empresaId ? Number(this.novoUsuario.empresaId) : null;
    }

    return Number(this.sessao()?.empresaId || this.novoUsuario.empresaId || this.empresas()[0]?.id || 1);
  }

  private async carregarDadosSistema(): Promise<void> {
    await Promise.all([
      this.carregarEmpresas(),
      this.carregarUsuarios(),
      this.carregarCategorias(),
      this.carregarChamados(),
      this.carregarBaseConhecimento(),
      this.carregarEnviosEquipamentos(),
      this.carregarInventario(),
      this.carregarLinks(),
    ]);
  }

  private async carregarEmpresas(): Promise<void> {
    try {
      const response = await fetch(`${this.apiUrl}/empresas`);
      if (!response.ok) {
        return;
      }

      const empresas = (await response.json()) as Array<Record<string, unknown>>;
      this.empresas.set(empresas.map((empresa) => this.mapearEmpresa(empresa)));
    } catch {
      return;
    }
  }

  private async carregarUsuarios(): Promise<void> {
    try {
      const response = await fetch(`${this.apiUrl}/usuarios${this.queryEmpresa()}`);
      if (!response.ok) {
        return;
      }

      const usuarios = (await response.json()) as Array<Record<string, unknown>>;
      this.usuarios.set(usuarios.map((usuario) => ({
        id: Number(usuario['id']),
        empresaId: usuario['empresaId'] == null ? undefined : Number(usuario['empresaId']),
        empresaNome: String(usuario['empresaNome'] ?? 'SaaS'),
        nome: String(usuario['nome'] ?? ''),
        email: String(usuario['email'] ?? ''),
        role: this.normalizarPerfil(usuario['role']),
        ativo: Boolean(usuario['ativo']),
      })));
    } catch {
      return;
    }
  }

  private async carregarChamados(): Promise<void> {
    try {
      const params = new URLSearchParams({
        perfil: this.perfil(),
        empresaId: String(this.sessao()?.empresaId ?? ''),
        usuarioId: String(this.sessao()?.id ?? ''),
        atendenteId: String(this.sessao()?.id ?? ''),
        solicitante: this.sessao()?.nome ?? '',
      });
      const response = await fetch(`${this.apiUrl}/chamados?${params.toString()}`);
      if (!response.ok) {
        return;
      }

      const chamados = (await response.json()) as Array<Record<string, unknown>>;
      this.chamados.set(chamados.map((chamado) => this.mapearChamado(chamado)));
      this.chamadoSelecionadoId.set(this.chamados()[0]?.id ?? 1);
    } catch {
      return;
    }
  }

  private async carregarCategorias(): Promise<void> {
    try {
      const response = await fetch(`${this.apiUrl}/categorias-chamado${this.queryEmpresa()}`);
      if (!response.ok) {
        return;
      }

      const categorias = (await response.json()) as Array<Record<string, unknown>>;
      if (!categorias.length) {
        return;
      }

      this.categorias.set(categorias.map((categoria) => ({
        id: Number(categoria['id']),
        empresaId: categoria['empresaId'] == null ? undefined : Number(categoria['empresaId']),
        nome: String(categoria['nome'] ?? ''),
        subcategorias: String(categoria['subcategorias'] ?? '')
          .split(',')
          .map((item) => item.trim())
          .filter(Boolean),
        prioridadePadrao: this.normalizarPrioridade(categoria['prioridadePadrao']),
        ativo: Boolean(categoria['ativo']),
      })));
    } catch {
      return;
    }
  }

  private async carregarBaseConhecimento(): Promise<void> {
    try {
      const response = await fetch(`${this.apiUrl}/base-conhecimento${this.queryEmpresa()}`);
      if (!response.ok) {
        return;
      }

      const artigos = (await response.json()) as Array<Record<string, unknown>>;
      this.artigos = artigos.map((artigo) => ({
        empresaId: artigo['empresaId'] == null ? undefined : Number(artigo['empresaId']),
        titulo: String(artigo['titulo'] ?? ''),
        categoria: String(artigo['categoria'] ?? ''),
        resumo: String(artigo['conteudo'] ?? ''),
        tags: Array.isArray(artigo['tags']) ? artigo['tags'].map(String) : [],
      }));
    } catch {
      return;
    }
  }

  private async carregarInventario(): Promise<void> {
    try {
      const response = await fetch(`${this.apiUrl}/equipamentos/inventario${this.queryEmpresa()}`);
      if (!response.ok) {
        return;
      }

      const inventario = (await response.json()) as Array<Record<string, unknown>>;
      this.inventario = inventario.map((item) => ({
        empresaId: item['empresaId'] == null ? undefined : Number(item['empresaId']),
        patrimonio: String(item['patrimonio'] ?? ''),
        hostname: String(item['hostname'] ?? ''),
        usuario: String(item['usuarioAtual'] ?? ''),
        filial: String(item['filial'] ?? ''),
        sistema: String(item['sistemaOperacional'] ?? ''),
        memoria: `${Number(item['memoriaGb'] ?? 0)} GB`,
      }));
    } catch {
      return;
    }
  }

  private async carregarEnviosEquipamentos(): Promise<void> {
    try {
      const response = await fetch(`${this.apiUrl}/equipamentos/envios${this.queryEmpresa()}`);
      if (!response.ok) {
        return;
      }

      const envios = (await response.json()) as Array<Record<string, unknown>>;
      this.envios = envios.map((item) => ({
        empresaId: item['empresaId'] == null ? undefined : Number(item['empresaId']),
        patrimonio: String(item['patrimonio'] ?? ''),
        tipo: String(item['tipo'] ?? ''),
        filialDestino: String(item['filialDestino'] ?? ''),
        responsavel: String(item['responsavelEnvio'] ?? ''),
        status: String(item['status'] ?? ''),
      }));
    } catch {
      return;
    }
  }

  private async carregarLinks(): Promise<void> {
    try {
      const response = await fetch(`${this.apiUrl}/links${this.queryEmpresa()}`);
      if (!response.ok) {
        return;
      }

      const links = (await response.json()) as Array<Record<string, unknown>>;
      this.links.set(links.map((link) => ({
        empresaId: link['empresaId'] == null ? undefined : Number(link['empresaId']),
        nome: String(link['nome'] ?? ''),
        firewall: String(link['firewall'] ?? ''),
        endereco: String(link['endereco'] ?? ''),
        intervalo: Number(link['intervaloLeituraSegundos'] ?? 60),
        disponivel: Boolean(link['disponivel']),
        chamado: link['chamadoAbertoId'] ? `#${link['chamadoAbertoId']}` : undefined,
      })));
    } catch {
      return;
    }
  }

  private mapearEmpresa(empresa: Record<string, unknown>): EmpresaLista {
    return {
      id: Number(empresa['id']),
      nome: String(empresa['nome'] ?? ''),
      razaoSocial: String(empresa['razaoSocial'] ?? empresa['nome'] ?? ''),
      nomeFantasia: String(empresa['nomeFantasia'] ?? empresa['nome'] ?? ''),
      tenantSlug: String(empresa['tenantSlug'] ?? ''),
      cnpj: String(empresa['cnpj'] ?? ''),
      telefone: String(empresa['telefone'] ?? ''),
      email: String(empresa['email'] ?? ''),
      endereco: String(empresa['endereco'] ?? ''),
      numero: String(empresa['numero'] ?? ''),
      complemento: String(empresa['complemento'] ?? ''),
      bairro: String(empresa['bairro'] ?? ''),
      cidade: String(empresa['cidade'] ?? ''),
      estado: String(empresa['estado'] ?? ''),
      cep: String(empresa['cep'] ?? ''),
      inscricaoMunicipal: String(empresa['inscricaoMunicipal'] ?? ''),
      inscricaoEstadual: String(empresa['inscricaoEstadual'] ?? ''),
      logoUrl: String(empresa['logoUrl'] ?? ''),
      ativo: Boolean(empresa['ativo']),
      acessoBloqueado: Boolean(empresa['acessoBloqueado']),
      motivoBloqueio: String(empresa['motivoBloqueio'] ?? ''),
      bloqueadoEm: String(empresa['bloqueadoEm'] ?? ''),
      dataCadastro: String(empresa['dataCadastro'] ?? '').slice(0, 10),
    };
  }

  private queryEmpresa(): string {
    if (this.perfil() === 'AdministradorSaas' || !this.sessao()?.empresaId) {
      return '';
    }

    return `?empresaId=${this.sessao()?.empresaId}`;
  }

  private mapearChamado(chamado: Record<string, unknown>): Chamado {
    const equipamento = chamado['equipamentoRelacionado'] as Record<string, unknown> | undefined;
    const anexos = Array.isArray(chamado['anexos']) ? chamado['anexos'] as Array<Record<string, unknown>> : [];
    const comunicacoes = Array.isArray(chamado['comunicacoes']) ? chamado['comunicacoes'] as Array<Record<string, unknown>> : [];

    return {
      id: Number(chamado['id']),
      empresaId: chamado['empresaId'] == null ? undefined : Number(chamado['empresaId']),
      solicitanteUsuarioId: chamado['solicitanteUsuarioId'] == null ? undefined : Number(chamado['solicitanteUsuarioId']),
      numero: String(chamado['numero'] ?? ''),
      titulo: String(chamado['titulo'] ?? chamado['assunto'] ?? ''),
      solicitante: String(chamado['solicitante'] ?? ''),
      categoria: String(chamado['categoria'] ?? ''),
      subcategoria: String(chamado['subcategoria'] ?? ''),
      tipo: this.normalizarTipoChamado(chamado['tipo']),
      prioridade: this.normalizarPrioridade(chamado['prioridade']),
      status: this.normalizarStatus(chamado['status']),
      descricao: String(chamado['descricao'] ?? ''),
      equipamento: equipamento
        ? `${String(equipamento['hostname'] ?? 'Portal Web')} | ${String(equipamento['sistemaOperacional'] ?? '')} | ${String(equipamento['ip'] ?? '')} | ${String(equipamento['navegador'] ?? '')}`
        : 'Equipamento nao informado',
      anexos: anexos.map((anexo) => String(anexo['nomeArquivo'] ?? anexo['url'] ?? 'Anexo')),
      atendente: chamado['atendenteNome'] ? String(chamado['atendenteNome']) : undefined,
      origem: chamado['origemAutomacao'] ? String(chamado['origemAutomacao']) : undefined,
      avaliacao: chamado['avaliacaoNota'] == null ? undefined : Number(chamado['avaliacaoNota']),
      mensagens: comunicacoes.map((mensagem) => ({
        autor: String(mensagem['autorNome'] ?? ''),
        perfil: this.normalizarPerfil(mensagem['autorPerfil']),
        texto: String(mensagem['mensagem'] ?? ''),
        horario: String(mensagem['enviadoEm'] ?? '').slice(11, 16) || 'Agora',
      })),
    };
  }

  private async lerMensagemErro(response: Response): Promise<string> {
    try {
      const data = (await response.json()) as { message?: string };
      return data.message ?? '';
    } catch {
      return '';
    }
  }

  private normalizarPerfil(valor: unknown): Perfil {
    return valor === 'AdministradorSaas' || valor === 'Administrador' || valor === 'Atendente' || valor === 'Usuario' ? valor : 'Usuario';
  }

  private normalizarTipoChamado(valor: unknown): Chamado['tipo'] {
    if (valor === 1 || valor === 'Solicitacao') {
      return 'Solicitacao';
    }

    if (valor === 2 || valor === 'Alteracao') {
      return 'Alteracao';
    }

    return 'Incidente';
  }

  private normalizarPrioridade(valor: unknown): Prioridade {
    if (valor === 0 || valor === 'Baixa') {
      return 'Baixa';
    }

    if (valor === 2 || valor === 'Alta') {
      return 'Alta';
    }

    if (valor === 3 || valor === 'Urgente') {
      return 'Urgente';
    }

    return 'Media';
  }

  private normalizarStatus(valor: unknown): Chamado['status'] {
    if (valor === 1 || valor === 'EmAtendimento' || valor === 'Em atendimento') {
      return 'Em atendimento';
    }

    if (valor === 2 || valor === 'Encerrado') {
      return 'Encerrado';
    }

    if (valor === 3 || valor === 'Cancelado') {
      return 'Cancelado';
    }

    return 'Aberto';
  }

  private async verificarAdministradorSaas(): Promise<void> {
    try {
      const response = await fetch(`${this.apiUrl}/auth/bootstrap-status`);
      if (!response.ok) {
        return;
      }

      const data = (await response.json()) as { existeAdministradorSaas: boolean };
      this.existeAdministradorSaas.set(data.existeAdministradorSaas);

      if (data.existeAdministradorSaas && this.authMode() === 'bootstrap') {
        this.authMode.set('login');
      }
    } catch {
      this.existeAdministradorSaas.set(true);
      this.authFeedback.set('Nao foi possivel verificar o primeiro acesso. Confirme se a API esta rodando.');
    }
  }

  private resetarFormularioEmpresa(): void {
    this.novaEmpresa = {
      id: undefined,
      nome: '',
      razaoSocial: '',
      nomeFantasia: '',
      tenantSlug: '',
      cnpj: '',
      telefone: '',
      email: '',
      endereco: '',
      numero: '',
      complemento: '',
      bairro: '',
      cidade: '',
      estado: '',
      cep: '',
      inscricaoMunicipal: '',
      inscricaoEstadual: '',
      logoUrl: '',
      ativo: true,
      acessoBloqueado: false,
      motivoBloqueio: '',
      bloqueadoEm: '',
      dataCadastro: '',
    };
  }

  protected selecionarPerfil(perfil: Perfil): void {
    this.perfil.set(perfil);
  }

  protected selecionarChamado(id: number): void {
    this.chamadoSelecionadoId.set(id);
  }

  protected async abrirChamado(): Promise<void> {
    await this.criarChamadoPortal();
  }

  protected async abrirChamadoDesenvolvedor(): Promise<void> {
    await this.criarChamadoPortal('Desenvolvedor');
  }

  private async criarChamadoPortal(origem?: string): Promise<void> {
    const anexos = this.novoChamado.anexos
      .split(',')
      .map((anexo) => anexo.trim())
      .filter(Boolean);
    const id = Math.max(0, ...this.chamados().map((chamado) => chamado.id)) + 1;
    const descricao = this.novoChamado.descricao || 'Chamado aberto pelo portal UniFlowIT.';
    const novoChamado = {
      empresaId: this.sessao()?.empresaId,
      solicitanteUsuarioId: this.sessao()?.id,
      titulo: this.novoChamado.titulo,
      solicitante: this.novoChamado.solicitante,
      categoria: this.novoChamado.categoria,
      subcategoria: this.novoChamado.subcategoria,
      tipo: this.novoChamado.tipo,
      prioridade: this.novoChamado.prioridade,
      descricao,
      origem,
      equipamentoRelacionado: this.criarEquipamentoCapturado(),
      anexos: anexos.map((anexo) => ({ nomeArquivo: anexo, tipoConteudo: '', tamanhoBytes: 0, url: '' })),
    };

    this.chamados.update((chamados) => [
      {
        id,
        empresaId: novoChamado.empresaId,
        solicitanteUsuarioId: novoChamado.solicitanteUsuarioId,
        numero: `CH-20260720-${String(id).padStart(4, '0')}`,
        titulo: novoChamado.titulo,
        solicitante: novoChamado.solicitante,
        categoria: novoChamado.categoria,
        subcategoria: novoChamado.subcategoria,
        tipo: novoChamado.tipo,
        prioridade: novoChamado.prioridade,
        status: 'Aberto',
        descricao,
        equipamento: this.capturarEquipamento(),
        anexos,
        origem,
        mensagens: [
          {
            autor: novoChamado.solicitante,
            perfil: 'Usuario',
            texto: descricao,
            horario: 'Agora',
          },
        ],
      },
      ...chamados,
    ]);

    this.chamadoSelecionadoId.set(id);
    this.novoChamado.titulo = '';
    this.novoChamado.descricao = '';
    this.novoChamado.anexos = '';

    try {
      await fetch(`${this.apiUrl}/chamados`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(novoChamado),
      });
      this.toastr.success('Chamado aberto com vinculo de empresa.', 'Central de chamados');
    } catch {
      this.toastr.info('Chamado criado localmente. API indisponivel para persistir agora.', 'Modo local');
    }
  }

  protected capturarChamado(chamado: Chamado): void {
    this.atualizarChamado(chamado.id, { status: 'Em atendimento', atendente: 'Rafael Nunes' });
  }

  protected encerrarChamado(chamado: Chamado): void {
    this.atualizarChamado(chamado.id, { status: 'Encerrado' });
  }

  protected cancelarChamado(chamado: Chamado): void {
    this.atualizarChamado(chamado.id, { status: 'Cancelado' });
  }

  protected enviarMensagem(textoMensagem?: string): void {
    const texto = (textoMensagem ?? this.novaMensagem).trim();
    if (!texto) {
      return;
    }

    const chamado = this.chamadoSelecionado();
    const perfil = this.perfil();
    const autor = perfil === 'Atendente' ? 'Rafael Nunes' : perfil === 'AdministradorSaas' ? 'Administrador SaaS' : perfil === 'Administrador' ? 'Administrador' : chamado.solicitante;

    this.atualizarChamado(chamado.id, {
      mensagens: [
        ...chamado.mensagens,
        {
          autor,
          perfil,
          texto,
          horario: 'Agora',
        },
      ],
    });
    this.novaMensagem = '';
  }

  protected avaliarChamado(chamado: Chamado, avaliacao = this.avaliacaoSelecionada): void {
    this.atualizarChamado(chamado.id, { avaliacao });
  }

  protected alternarLink(link: LinkMonitorado): void {
    this.links.update((links) =>
      links.map((item) => {
        if (item.nome !== link.nome) {
          return item;
        }

        if (item.disponivel) {
          const novoChamado = this.criarChamadoLink(item);
          this.chamados.update((chamados) => [novoChamado, ...chamados]);
          return { ...item, disponivel: false, chamado: novoChamado.numero };
        }

        return { ...item, disponivel: true };
      }),
    );
  }

  private criarChamadoLink(link: LinkMonitorado): Chamado {
    const id = Math.max(...this.chamados().map((chamado) => chamado.id)) + 1;

    return {
      id,
      empresaId: this.sessao()?.empresaId,
      numero: `CH-20260720-${String(id).padStart(4, '0')}`,
      titulo: `Link indisponivel: ${link.nome}`,
      solicitante: 'Monitoramento de links',
      categoria: 'Infraestrutura',
      subcategoria: 'Link indisponivel',
      tipo: 'Incidente',
      prioridade: 'Urgente',
      status: 'Aberto',
      descricao: `Link indisponivel: ${link.nome} (${link.endereco}).`,
      equipamento: `${link.firewall} | ${link.endereco} | leitura ${link.intervalo}s`,
      anexos: [],
      origem: 'Monitoramento automatico',
      mensagens: [],
    };
  }

  private atualizarChamado(id: number, patch: Partial<Chamado>): void {
    this.chamados.update((chamados) => chamados.map((chamado) => (chamado.id === id ? { ...chamado, ...patch } : chamado)));
  }

  private capturarEquipamento(): string {
    const equipamento = this.criarEquipamentoCapturado();
    return `Portal Web | ${equipamento.sistemaOperacional} | ${equipamento.navegador}`;
  }

  private criarEquipamentoCapturado(): { hostname: string; sistemaOperacional: string; usuarioLogado: string; ip: string; navegador: string } {
    const navegador = navigator.userAgent.includes('Edg') ? 'Edge' : navigator.userAgent.includes('Chrome') ? 'Chrome' : 'Navegador detectado';
    return {
      hostname: 'Portal Web',
      sistemaOperacional: navigator.platform || 'Sistema nao identificado',
      usuarioLogado: this.sessao()?.nome ?? this.novoChamado.solicitante,
      ip: '',
      navegador,
    };
  }
}
