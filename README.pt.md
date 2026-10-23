# DayKeep

[简体中文](README.md) · [繁體中文](README.zh-TW.md) · [English](README.en.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Español](README.es.md) · [Português](README.pt.md)

[Baixar ZIP para Windows](https://github.com/Thicky-han/DayKeep/releases/download/v0.7.0-beta.1/DayKeep-0.7.0-beta.1-win-x64.zip) · [Código-fonte](https://github.com/Thicky-han/DayKeep/releases/download/v0.7.0-beta.1/DayKeep-0.7.0-beta.1-source.zip) · [Somas de verificação](https://github.com/Thicky-han/DayKeep/releases/download/v0.7.0-beta.1/SHA256SUMS) · [Lançamento](https://github.com/Thicky-han/DayKeep/releases/tag/v0.7.0-beta.1)

Um passo por dia, até concluir. Lembretes locais para Windows de tarefas que exigem verificações, como um portal de pagamento de patente ainda fechado. Verificar hoje não conclui a tarefa inteira. A versão pública de pré-lançamento atual **0.7.0-beta.1** teve sua publicação aprovada pelo responsável e adiciona nove idiomas de interface e um seletor visível. Os links acima oferecem esta versão, fontes e somas; os resultados de compilação, empacotamento e o andamento das verificações públicas estão no [relatório da atualização de idiomas](LANGUAGE_UPDATE_REPORT.md). A anterior **0.6.0-l6** só tem interface em chinês simplificado; seus anexos e tag originais permanecem intactos. Os documentos congelados no pacote aprovado refletem o estado de candidata na hora do empacotamento. O teste real de três dias continua pendente. Binários sem assinatura.

![Janela com tarefas fictícias](assets/screenshots/main.png)

## Alterar o idioma da interface

Abra **Language / 语言** no topo da janela principal. O seletor permanece visível nos modos amplo e compacto, sem precisar entrar nas configurações. Cada idioma aparece com seu próprio nome: **简体中文, 繁體中文, English, 日本語, 한국어, Français, Deutsch, Español, Português**.

A escolha atualiza imediatamente os textos da janela principal, formulários, lembretes, histórico e backups. É salva neste computador e restaurada na próxima abertura. Sem escolha salva, inclusive ao atualizar, o padrão é chinês simplificado. Se não for possível salvar, um erro aparece e o idioma anterior é mantido.

Seus nomes de tarefas, notas, links e registros originais não são traduzidos. A troca não muda o fuso local do computador, dias ou horários programados. Continue usando `HH:mm` de 24 horas e `yyyy-MM-dd` para prazos. Os nomes chineses abaixo são referências; o app exibe o equivalente no idioma escolhido. [Idiomas e salvamento](docs/LANGUAGES.md)

## Instalar e atualizar

Extraia todo DayKeep-0.7.0-beta.1-win-x64.zip. O pacote antigo 0.6.0-l6 funciona da mesma forma, mas sem seletor de idioma. Windows x64, runtime .NET incluído, sem SDK. Não execute dentro do ZIP nem copie apenas o exe. Confira origem e SHA256SUMS; abra DayKeep.exe para uso portátil. O Windows pode mostrar editor desconhecido.

Install.cmd instala sem administrador em LocalApplicationData/Programs/DayKeep/0.7.0-beta.1 e cria atalhos DayKeep na área de trabalho e no menu Iniciar. A exceção de política PowerShell vale somente para esse processo. O início no login fica desligado. Antes de atualizar, exporte backup, salve formulários e encerre a versão antiga por「退出日守」na bandeja; × apenas oculta. Cada versão usa pasta separada; pastas/atalhos desconhecidos são preservados. Reinstalar a mesma versão exige arquivos idênticos e íntegros. O primeiro início migra por transação para schema 5; L1–L4 não leem a base atualizada.

## Primeira tarefa

1. 「＋ 新建任务」: escreva “Verificar pagamento da patente”.「等待条件」para aguardar disponibilidade;「普通待办」para algo já executável.
2. URL completa http/https e notas são opcionais. URLs com credenciais são rejeitadas. Prazo desconhecido pode ficar vazio.
3. `09:00` ou `09:00, 14:30`, HH:mm de 24 horas, até oito horários distintos; selecione pelo menos um dia.
4. Padrão: repetir a cada 30 minutos sem tratamento, no máximo três extras por dia local. Intervalo 5–1440 minutos, limite 0–24. Zero desativa extras, mantém horários fixos.
5. Prazo conhecido: `yyyy-MM-dd`. Hoje/atrasado recebe destaque e prioridade, sem conclusão automática. Salve e mantenha o app na bandeja.

## Acompanhar diariamente

| Botão (referência em chinês) | Resultado |
| --- | --- |
| 打开平台 | Abre apenas navegador; não registra verificação ou conclusão |
| 今日已检查，尚未开放 | Verificado, ainda fechado: para verificações de hoje; retoma no próximo dia escolhido |
| 已开放，转待办理 | Disponível: passa a pronto para tratar e mantém acompanhamento |
| 10 分钟后 / 30 分钟后 / 1 小时后 | Adia esta tarefa 10/30/60 minutos, cobre avisos intermediários e persiste após reinício |
| 确认已完成 | Confirma após realizar; para lembretes, permite desfazer |

Tarefas simultâneas se juntam numa janela superior rolável, sem pedir foco do teclado nem sumir após segundos. Fechar não conclui nada. Após 09:00, o padrão é 09:30, 10:00, 10:30. Coincidência com horário fixo se agrupa sem gastar extra; adiamento explícito também não consome limite. A verificação diária cancela os avisos de espera restantes do dia.

「暂停」pausa preservando estado;「归档」arquiva sem excluir. Restaure pelos filtros.「撤销完成」recupera estado e pausa anteriores mesmo após reiniciar.

## Área de trabalho e histórico

「窄窗 / 展开」muda largura;「置顶」fixa no topo. Transparência só no fundo. ×/Esc oculta; Ctrl+Alt+D, clique na bandeja ou novo início reabre. Use a bandeja se o atalho conflitar. Ocultar preserva formulários, fechar o formulário descarta rascunho.「退出日守」encerra realmente. Não há aviso com app parado ou computador desligado.

Reinício, retorno da suspensão e desbloqueio recuperam só o último horário fixo perdido válido por tarefa. Avisos não recolhidos voltam; recolhidos não duplicam. Outro app em tela cheia ou área de trabalho indisponível adia a exibição.「登录 Windows 后在托盘运行」fica desligado por padrão; confira após mudar de pasta. Windows pode atrasar/desativar esse início.

Abra「历史与备份」ou, em modo compacto,「检查历史与备份」na parte inferior. Escolha tarefa incluindo concluídas/arquivadas, datas inclusivas e texto de nome/ação/nota.「查询」consulta,「重置」limpa; vazio não limita. Mostra os 200 resultados mais recentes; restrinja para anteriores. Backup inclui tudo. Nome atual, data local original da verificação, horário no fuso atual.

## Backup, restauração e desfazer importação

1. 「备份与恢复 → 导出完整备份」salva novo `.daykeep.json`. Não sobrescreve; formulários não salvos ficam fora.
2. Inclui tarefas, histórico, desfazer conclusão, arquivo, adiamentos, limites diários e entregas; exclui idioma/aparência/início. Importar não muda o idioma escolhido. Texto sem criptografia: guarde em local privado e importe apenas fontes confiáveis.
3. 「选择备份并预览」exibe data/contagens sem alteração.「合并」adiciona só GUID ausentes localmente; mesmo ID mantém todo grupo local. Mesmo nome com ID diferente permanece separado.
4. 「替换」restaura backup inteiro e remove tarefas locais ausentes. Salve/feche formulários, revise impacto e marque confirmação. Trocar modo ou idioma desmarca. Ao trocar idioma, arquivo, modo e prévia congelada são preservados; confira o impacto no novo idioma e confirme novamente.
5. Se dados/entregas mudarem,「重新预览」. Antes de gravar, o estado atual vai automaticamente para Recovery junto a tasks.db; o caminho aparece após sucesso.
6. Para desfazer, visualize esse arquivo e substitua. Falha no backup de recuperação impede alteração; falha SQL reverte transação. Corrupção, versão, estrutura ou hash incompatível são rejeitados.

Limites: 64 MiB, 20.000 tarefas, 200.000 registros no total. SHA-256 detecta danos, não autentica nem criptografa. Lembretes pendentes restaurados podem aparecer imediatamente.[Contrato](docs/BACKUP_FORMAT.md).

## Dados, desinstalação e ajuda

LocalApplicationData/DayKeep contém tasks.db e ui-settings.json. Trocar programa não apaga dados. Cópia manual: encerre e copie pasta inteira com arquivos auxiliares. Base corrompida: preserve cópia completa, renomeie original, abra base vazia e importe backup confiável.

Desative início no login, salve e saia pela bandeja; execute **Uninstall.cmd da pasta instalada**. Remove somente arquivos íntegros listados e seus atalhos; dados pessoais e outras versões permanecem. Arquivos extras/alterados interrompem remoção. Pasta portátil dispensa desinstalador.

Sem aviso: confira bandeja, hora/dia, pausa/arquivo/conclusão, verificação de hoje e tela cheia. Versão antiga abre: encerre aquela. Erro de salvar/importar: preserve arquivos, confira permissões/espaço/bloqueio e refaça prévia. Sem conta, telemetria, captura web, senhas ou pagamento automático.

Três dias reais, suspensão/bloqueio reais, monitores múltiplos/DPI mistos e tela cheia exclusiva permanecem pendentes. A última observação real L4 foi adiada por disponibilidade da área de trabalho; simulações não substituem esses testes.

[Lista de teste](docs/TRIAL_CHECKLIST.md) · [Guia chinês](INSTALL.md) · [Desenvolvimento em inglês](README.en.md) · [Privacidade](PRIVACY.md)

Código, documentos e logo aprovado: AGPL-3.0-only. Distribua LICENSE e fonte completo correspondente. Licenças de terceiros em licenses do pacote.[Relatório L6](L6_REPORT.md)
