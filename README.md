# NexBoost Gaming

Aplicação **independente** do NexBoost original, feita em C# / Windows Forms / .NET 8 para Windows 10/11.

## Funcionalidades implementadas
- Adicionar, remover, guardar e iniciar jogos `.exe`.
- Botão **BOOST GAME**: ativa o plano de energia Alto Desempenho **se disponível**; guarda e restaura o plano anterior, incluindo ao fechar a aplicação. Se ocorrer uma falha, guarda o identificador para recuperação no próximo arranque.
- Monitorização leve de **CPU e RAM** (aproximada, a cada 1,5 segundos).
- Mini overlay móvel e sempre visível com CPU, RAM e campo FPS.
- Interface escura com azul-neon, fonte Bahnschrift SemiCondensed e ícone próprio.

## Limitações importantes
- **FPS: N/D**. Captura de FPS real não está implementada nesta versão. Exige integração com ferramenta externa (por exemplo, PresentMon). A overlay apresenta o campo para futura integração, mas **não inventa valores**.
- **GPU: N/D**. A monitorização de utilização da GPU não está implementada sem uma biblioteca adicional.
- O Boost **não** altera prioridades de processos, serviços do Windows, antivírus ou ficheiros críticos. Pode não produzir mais FPS. Alguns computadores não disponibilizam o plano Alto Desempenho.
- O jogo selecionado não é monitorizado automaticamente; o botão Boost aplica o perfil de energia ao sistema até ser revertido.
- Pode ser necessário iniciar com permissões elevadas para certas configurações de energia. O programa não solicita elevação automática.
- Este projeto não foi compilado/testado num PC Windows nesta sessão. A compilação é feita no GitHub Actions.

## GitHub
1. Cria um repositório vazio, por exemplo `NexBoost-Gaming`.
2. Carrega **o conteúdo** desta pasta na raiz do repositório, incluindo `.github/workflows/build-windows.yml`.
3. Faz commit no ramo `main`.
4. Abre **Actions → Build NexBoost Gaming** e, quando terminar, descarrega `NexBoostGaming-Windows-x64` em Artifacts.
5. Extrai o ZIP de artefacto e inicia `NexBoostGaming.exe`.

## Compilação local
```powershell
dotnet publish NexBoostGaming.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```
