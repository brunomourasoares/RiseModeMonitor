# HANDOFF — Rise Mode Monitor

> Arquivo de controle compartilhado entre quem trabalha no projeto (pessoas e agentes).
> **Leia antes de começar. Atualize antes de parar.** Última atualização: 2026-11-01 (`main` em `c00d753`).

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
| Ventoinhas | Controle manual (a barra não acompanha o RPM); canal que nunca girou fica oculto; ventoinha que já girou e parou mostra aviso e o botão *Esquecer* | `FansViewModel`, `FansView.xaml`, `AppSettings.FansSeenSpinning` |
| Alertas | CPU/GPU, ventoinha parada, 12 sons, volume e intervalo entre os toques (3–120 s, padrão 10 s); o som toca até normalizar | `AlertEvaluator`, `AlertService`, `AlertSettingsViewModel` |
| Configurações | Tema, 6 idiomas com sigla (ex.: Português (BR)), iniciar com o Windows, bandeja, visor; switches e ícones nos cards | `SettingsView.xaml`, `SettingsViewModel` |
| Interface | Menu lateral como card; ícones como geometrias XAML; fonte Cyberiada no título | `MainWindow.xaml`, `Resources/Icons.xaml`, `Resources/Fonts` |
| Idiomas | en, pt, es, it, fr, zh; as 6 devem ter as mesmas chaves | `Resources/Strings*.resx` |
| Iluminação | **Não existe**: a página é um espaço reservado | `PlaceholderViewModel` |

## Decisões tomadas

- **Armazenamento:** um card por disco físico, com o modelo e uma linha por volume (`C: 209,5 GB / 232,3 GB - 22,8 GB livres`). Drives que o WMI não liga a um disco aparecem em card próprio.
- **Ventoinha em uso:** canal que nunca girou fica oculto. Um que já girou (histórico salvo em `FansSeenSpinning`) continua visível e mostra aviso depois de 3 leituras seguidas de 0 RPM. *Esquecer* tira a ventoinha do histórico e do alerta; ela volta sozinha se girar de novo.
- **Alarme sonoro:** a opção é o *intervalo entre os toques* (3–120 s, padrão 10 s). O alarme não para por tempo: só quando tudo volta ao normal.
- **Idiomas e siglas:** English (US), Português (BR), Español (ES), Italiano (IT), Français (FR), 简体中文 (CN).
- **Fora de escopo (decisão do dono do projeto):** licença e atribuição dos ícones e da fonte Cyberiada.
- **Log:** por enquanto só `Debug.WriteLine` nos `catch` que antes eram silenciosos. Um log em arquivo para o usuário final não foi decidido (o `HidDisplayService` já tem o seu).

## Em andamento

_Ninguém no momento._

## Em discussão (ainda não implementado)

- **Switch de alerta por ventoinha + switch geral para ocultar as desativadas** (ideia do dono do projeto). Pontos a decidir antes de codar:
  - o que "desativada" significa: só sem alerta, ou também sem aparecer;
  - se substitui o botão *Esquecer* e o histórico `FansSeenSpinning`;
  - o que acontece com uma ventoinha que já nasce parada no boot (hoje o alerta só vale para ventoinhas vistas girando na sessão);
  - onde guardar o estado por canal (`AppSettings`) e os textos novos nos 6 idiomas.

## Pendências

- [ ] **Testar o app no Windows** (o CI só compila): menu lateral, Configurações (switches, intervalo do alarme), ventoinhas ocultas, aviso e botão *Esquecer*, barra manual, cards de armazenamento.
- [ ] Conferir o CI do push direto no `main` (`c00d753`, traduções it/fr/zh).
- [ ] Alerta para ventoinha que já nasce parada no boot (ver *Em discussão*).
- [ ] Iluminação RGB/ARGB e curvas de ventoinha (listadas no README como pendentes).
- [ ] Log de erros em arquivo, se for desejado.
- [ ] Apagar os branches antigos já mesclados (`feat/*`, `fix/ui-fans-followups`, `docs/readme-update`).

## Log

| Data | Quem | O que foi feito |
|---|---|---|
| 2026-11-01 | Claude | `HANDOFF.md` vai para o repositório. Botão *Esquecer* (PR #23 + `c00d753` com as traduções it/fr/zh que ficaram fora do merge). README e log mínimo (PR #22). |
| 2026-10-30 | Claude | PR #21: card do menu, histórico de ventoinhas com aviso, intervalo entre toques, título localizado do erro, cabeçalho do `Icons.xaml` sem Font Awesome. |
| 2026-10-29 | Claude | PR #20: cards de armazenamento por disco, redesign das Configurações (switches, ícones, idiomas com sigla), ventoinhas (barra manual e canais sem uso), menu e rolagem. |
