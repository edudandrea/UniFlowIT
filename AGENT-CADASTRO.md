# Cadastro pelo agente

No acesso do **administrador da empresa**, clique no menu em
**Baixar agente da empresa**. O download usa a empresa da sessao autenticada;
nao e disponibilizado no acesso SaaS e nao permite selecionar outra empresa.
Gere um arquivo para cada usuario. Execute o `.ps1` pelo PowerShell no computador do
usuario; ele baixa e abre o instalador oficial servido pela API.

A autorizacao de cadastro vale por sete dias e permite criar somente um usuario,
sempre com perfil `Usuario` e vinculado a empresa logada. A autorizacao assinada
tambem preserva a contratante cadastrada (`EmpresaContratanteId`, quando houver).
Se esse vinculo mudar, gere um novo instalador. O projeto ainda nao possui um
cadastro separado de revenda.
O computador nao recebe a sessao administrativa. Uma empresa inativa ou bloqueada
nao permite cadastro. O instalador e o pacote do agente atualizados precisam estar
disponiveis na API antes de distribuir os arquivos.

Apos a instalacao, o agente abre `http://127.0.0.1:17891/cadastro` no navegador e
solicita nome, e-mail, telefone de contato, ramal e senha. Todos sao obrigatorios.
A senha segue a regra atual: pelo menos oito caracteres, letra maiuscula, numero
e caractere especial. O e-mail sera o login do portal de chamados.

Se o cadastro for fechado ou a API estiver indisponivel, o usuario pode reabrir
o endereco local. O agente tambem reabre o cadastro no proximo inicio enquanto
houver autorizacao pendente. A senha nao e salva no computador nem em logs;
o servidor armazena seu hash. Apos cadastrar, o agente guarda a sessao do usuario
e remove a autorizacao de cadastro. Repetir a mesma solicitacao apos uma falha
de comunicacao recupera o resultado, sem criar outro usuario.

O login existente reconhece o perfil `Usuario` e abre a tela de chamados.
Na sincronizacao, a API rejeita uma empresa diferente da empresa autenticada,
um ID de equipamento de outra empresa/unidade ou um agente ja associado a outra
empresa/unidade. Equipamentos com o mesmo hostname em empresas distintas nao sao
mesclados. O token de sessao deixa de valer se o usuario mudar de empresa.
O acesso remoto passa por autorizacao na API, que exige administrador ou atendente
da empresa e equipamento vinculado a essa mesma empresa. O agente local tambem
valida a solicitacao usando sua empresa configurada; IDs e senhas enviados pelo
navegador nao autorizam acesso por si mesmos. Uma negativa de autorizacao bloqueia
o acionamento do RustDesk. O download generico automatico no login foi removido.
Uma conta ja existente nao tem sua senha alterada por esse cadastro: nesse caso,
use o acesso existente e contate o administrador para configurar o agente.

A migracao `AddAgentUserContact` acrescenta telefone, ramal e o identificador da
autorizacao utilizada. A API aplica suas migracoes no inicio, conforme o fluxo
atual do projeto. A sessao do agente continua sujeita a validade atual de oito
horas; um novo login no portal renova a configuracao pelo fluxo ja existente.

Validacao automatizada do cadastro local e das credenciais:

```powershell
dotnet run --project tests/AgentEnrollmentChecks
```
