# HANDOFF — Rise Mode Monitor

> Arquivo de controle compartilhado entre quem trabalha no projeto (pessoas e agentes).
> **Leia antes de começar. Atualize antes de parar.** Última atualização: 2026-11-02 (`main` em `bcb5ae0`).

## Como usar

1. Antes de trabalhar: leia este arquivo e o `README.md`.
2. Ao pegar uma tarefa: escreva em *Em andamento* quem está com ela e em qual branch.
3. Ao terminar ou pausar: mova a tarefa, atualize *Estado atual* e acrescente uma linha no *Log*.
4. Não mexa no que está *Em andamento* de outra pessoa sem avisar aqui.
5. Prefira branch + pull request: o workflow **Build** roda em todo PR. Ele só compila, não executa o app.

## Contexto

App Windows (C# / WPF, .NET 8) para monitorar sensores e controlar ventoinhas, RGB/ARGB e o visor do water cooler Rise Mode. Detalhes de stack, estrutura e requisitos estão no [`README.md`](README.md); o protocolo do visor está em [`docs/display-protocol.md`](docs/display-protocol.md).

```
dotnet build RiseModeMonitor.sln -c Release   # o app exige administrador para rodar
```

## Estado atual

| Área | O que existe | Onde está |
|---|---|---|
| Painel | CPU, GPU, memória e armazenamento com um card por disco físico (modelo e volumes, atualizados no lugar) | `DashboardViewModel`, `WmiStorageCatalog` |
| Hardware | Sensores por dispositivo, busca, mínimo e máximo | `HardwareViewModel`, `HardwareView.xaml` |
| Ventoinhas | Controle manual (a barra não acompanha o RPM); switch de alerta em cada card e um switch geral para ocultar as ventoinhas com o alerta desligado; aviso no card de uma ventoinha com alerta ligado que parou | `FansViewModel`, `FansView.xaml`, `AppSettings.FanAlerts` / `HideDisabledFans` / `FansSeenSpinning` |
| Alertas | CPU/GPU, ventoinha parada, 12 sons, volume e intervalo entre os toques (3–120 s, padrão 10 s); o som toca até normalizar | `AlertEvaluator`, `AlertService`, `AlertSettingsViewModel` |
| Configurações | Tema, 6 idiomas com sigla (ex.: Português (BR)), iniciar com o Windows, bandeja, visor; switches e ícones nos cards | `SettingsView.xaml`, `SettingsViewModel` |
| Interface | Menu lateral como card; ícones como geometrias XAML; fonte Cyberiada no título | `MainWindow.xaml`, `Resources/Icons.xaml`, `Resources/Fonts` |
| Idiomas | en, pt, es, it, fr, zh; as 6 devem ter as mesmas chaves | `Resources/Strings*.resx` |
| Iluminação | **Não existe**: a página é um espaço reservado | `PlaceholderViewModel` |

## Decisões tomadas

- **Armazenamento:** um card por disco físico, com o modelo e uma linha por volume (`C: 209,5 GB / 232,3 GB - 22,8 GB livres`). Drives que o WMI não liga a um disco aparecem em card próprio.
- **Alerta por ventoinha:** cada canal tem um switch (`AppSettings.FanAlerts`). Sem escolha do usuário, o padrão é ligado para canal que já girou (histórico em `FansSeenSpinning`) e desligado para canal que nunca girou (cabeçalho vazio). Um switch geral (`HideDisabledFans`, padrão ligado) oculta os cards com o alerta desligado; o card sempre aparece em modo manual. O alerta cobre também a ventoinha que já nasce parada no boot, depois de 10 leituras seguidas de 0 RPM (3 se ela já girou na sessão). O botão *Esquecer* foi removido: desligar o switch faz o mesmo e é reversível.
- **Alarme sonoro:** a opção é o *intervalo entre os toques* (3–120 s, padrão 10 s). O alarme não para por tempo: só quando tudo volta ao normal.
- **Fonte das traduções:** o português é a língua de origem. Ao mudar um texto, mude primeiro o `Strings.pt.resx` e derive as outras 5 línguas dele (mesmas chaves, mesmo sentido, mesmos termos, por exemplo *water cooler*). Cuidado para não aplicar a tradução na chave errada: o título de um card (`Settings_Display`) não é o texto do seu switch (`Settings_DisplayEnabled`).
- **Idiomas e siglas:** English (US), Português (BR), Español (ES), Italiano (IT), Français (FR), 简体中文 (CN).
- **Fora de escopo (decisão do dono do projeto):** licença e atribuição dos ícones e da fonte Cyberiada.
- **Log:** por enquanto só `Debug.WriteLine` nos `catch` que antes eram silenciosos. Um log em arquivo para o usuário final não foi decidido (o `HidDisplayService` já tem o seu).

## Em andamento

_Ninguém no momento._

## Em discussão (ainda não implementado)

_Nada no momento._

## Pendências

- [ ] **Testar o app no Windows** (o CI só compila): menu lateral, Configurações (switches, intervalo do alarme), switches de alerta por ventoinha e o geral de ocultar, aviso de ventoinha parada, barra manual, cards de armazenamento.
- [ ] Conferir o CI dos pushes diretos no `main` (`c00d753` e `bcb5ae0`, traduções it/fr/zh).
- [ ] Testar o alerta de ventoinha que já nasce parada no boot (puxar o cabo de uma ventoinha com o PC desligado e ligar de novo).
- [ ] Iluminação RGB/ARGB e curvas de ventoinha (listadas no README como pendentes).
- [ ] Log de erros em arquivo, se for desejado.
- [ ] Apagar os branches antigos já mesclados (`feat/*`, `fix/ui-fans-followups`, `docs/readme-update`).

## Log

| Data | Quem | O que foi feito |
|---|---|---|
| 2026-11-02 | Claude | Switch de alerta por ventoinha e switch geral de ocultar (PR #24 + `bcb5ae0` com as traduções it/fr/zh que ficaram fora do merge); o alerta passa a cobrir ventoinha que nasce parada; botão *Esquecer* removido. README e este arquivo atualizados. |
| 2026-11-01 | Claude | `HANDOFF.md` vai para o repositório. Botão *Esquecer* (PR #23 + `c00d753` com as traduções it/fr/zh que ficaram fora do merge). README e log mínimo (PR #22). |
| 2026-10-30 | Claude | PR #21: card do menu, histórico de ventoinhas com aviso, intervalo entre toques, título localizado do erro, cabeçalho do `Icons.xaml` sem Font Awesome. |
| 2026-10-29 | Claude | PR #20: cards de armazenamento por disco, redesign das Configurações (switches, ícones, idiomas com sigla), ventoinhas (barra manual e canais sem uso), menu e rolagem. |
