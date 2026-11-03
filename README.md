# Rise Mode Monitor

Aplicativo Windows (C# / WPF) para monitorar sensores e controlar ventoinhas, iluminação RGB/ARGB e o visor do bloco do water cooler em hardware **Rise Mode**.

## Funcionalidades

- **Painel:** CPU, GPU, memória e armazenamento (um card por disco físico, com o modelo e os volumes).
- **Hardware:** sensores de cada dispositivo, com busca, mínimo e máximo.
- **Ventoinhas:** controle manual por canal (velocidades abaixo de 20% são bloqueadas) e nome editável por canal. Cada ventoinha tem um switch de alerta (ligado por padrão para as que já giraram) e há um switch geral para ocultar as que estão com o alerta desligado; uma ventoinha com o alerta ligado que para mostra um aviso. Ao fechar o app, todas voltam ao controle automático.
- **Alertas:** temperatura de CPU e GPU e ventoinha parada, com notificação, 12 sons, volume e intervalo entre os toques (3 a 120 s).
- **Configurações:** tema (sistema, claro ou escuro), 6 idiomas (en, pt, es, it, fr, zh), iniciar com o Windows (tarefa agendada, sem aviso do UAC), iniciar minimizado, bandeja do sistema e envio de dados ao visor do water cooler.
- **Visor:** envio de dados do sistema ao bloco do water cooler (modelos RM-WAI-0X-ARGB e RM-WAIB-0X-ARGB). O protocolo está descrito em [`docs/display-protocol.md`](docs/display-protocol.md).

## Stack

- .NET 8, WPF, WPF-UI, MVVM (CommunityToolkit.Mvvm), injeção de dependência (Microsoft.Extensions.DependencyInjection)
- [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) 0.9.6 com driver **PawnIO**
- HidSharp para o visor (HID)
- System.Management (WMI) para ligar cada letra de unidade ao disco físico

## Estrutura

| Projeto | Responsabilidade |
|---|---|
| `RiseModeMonitor.Core` | Modelos e interfaces (`ISensorService`, `IFanControlService`, `IDisplayService`, `IStorageCatalog`, `ISettingsService`) e a lógica de alertas |
| `RiseModeMonitor.Hardware` | Leitura de sensores e controle de ventoinhas via LibreHardwareMonitor; catálogo de discos via WMI |
| `RiseModeMonitor.Hid` | Comunicação HID com o visor do water cooler |
| `RiseModeMonitor.App` | Interface WPF (Painel, Hardware, Ventoinhas, Iluminação, Configurações), bandeja, alertas e autostart |

## Requisitos

- Windows 10/11 x64, SDK .NET 8, Visual Studio 2022 (workload *.NET desktop development*)
- Driver **PawnIO** instalado (necessário para a LibreHardwareMonitorLib 0.9.x ler a maioria dos sensores)
- O app usa `requireAdministrator` no manifesto: **abra o Visual Studio como administrador** para depurar.

## Build e CI

```
dotnet build RiseModeMonitor.sln -c Release
```

O workflow **Build** (GitHub Actions, `windows-latest`) compila a solução em Release a cada pull request e a cada push na `main`. Ele só compila: não executa o app nem testes.

## Estado atual

- [x] Solução e projetos
- [x] Serviço de sensores (polling a cada 1 s)
- [x] Painel, Hardware, Ventoinhas e Configurações
- [x] Temas claro/escuro, idiomas, autostart e bandeja
- [x] Alertas sonoros e notificações
- [x] Envio de dados ao visor HID
- [ ] Iluminação RGB/ARGB (a página ainda é um espaço reservado)
- [ ] Curvas de ventoinha (hoje só há controle manual)
- [ ] Log de erros (vários `catch` ainda engolem exceções em silêncio)
