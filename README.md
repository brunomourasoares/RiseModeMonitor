# Rise Mode Monitor

Aplicativo Windows (C# / WPF) para monitorar sensores e controlar ventoinhas, iluminação RGB/ARGB e o visor do bloco do water cooler em hardware **Rise Mode**.

## Stack

- .NET 8, WPF, MVVM (CommunityToolkit.Mvvm), injeção de dependência (Microsoft.Extensions.DependencyInjection)
- [LibreHardwareMonitorLib](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) 0.9.6 com driver **PawnIO**
- HidSharp para o visor (HID)

## Estrutura

| Projeto | Responsabilidade |
|---|---|
| `RiseModeMonitor.Core` | Modelos e interfaces (`ISensorService`, `IDisplayService`) |
| `RiseModeMonitor.Hardware` | Leitura de sensores via LibreHardwareMonitor |
| `RiseModeMonitor.Hid` | Comunicação HID com o visor do water cooler |
| `RiseModeMonitor.App` | Interface WPF (Dashboard, Hardware, Ventoinhas, Iluminação, Configurações) |

## Requisitos

- Windows 10/11 x64, SDK .NET 8, Visual Studio 2022 (workload *.NET desktop development*)
- Driver **PawnIO** instalado (necessário para a LibreHardwareMonitorLib 0.9.x ler a maioria dos sensores)
- O app usa `requireAdministrator` no manifesto: **abra o Visual Studio como administrador** para depurar.

## Build

```
dotnet build RiseModeMonitor.sln -c Release
```

## Estado atual

- [x] Solução e projetos
- [x] Serviço de sensores (polling a cada 1 s)
- [x] Dashboard básico (CPU, GPU, RAM, discos)
- [x] Temas claro/escuro (dicionários) — paleta ainda provisória
- [ ] Hardware completo, Ventoinhas, RGB/ARGB, Configurações, Idiomas, Autostart, Visor HID (ver issues)
