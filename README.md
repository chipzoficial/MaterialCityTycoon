# Material City Tycoon

Material City Tycoon é um tycoon de gestão comercial e construção. O objetivo é desenvolver, por incrementos, uma loja de materiais de construção que possa crescer em resposta à demanda da cidade.

## Estado atual

O projeto está no marco **M0 — pré-produção**. Nesta fase, a base técnica, a documentação e o versionamento são preparados antes da implementação de gameplay.

## Ferramentas

- Unity 6 com Universal Render Pipeline (URP)
- C#
- Visual Studio Code
- Git

As versões confirmadas do Editor e dos pacotes estão em [Documentation/TECH_STACK.md](Documentation/TECH_STACK.md).

## Como abrir

1. Abra o Unity Hub.
2. Selecione **Add** e escolha a pasta raiz deste repositório.
3. Abra o projeto com a versão do Unity indicada em `ProjectSettings/ProjectVersion.txt`.
4. Aguarde a importação dos assets e pacotes terminar.

## Escopo técnico

O documento de referência está em [Documentation/Escopo_Tecnico_v0.3.md](Documentation/Escopo_Tecnico_v0.3.md).

## Validação manual mínima

1. Abra o projeto no Unity sem solicitar atualização de versão ou de pacotes.
2. Confirme no Package Manager que o Universal RP e o Input System estão instalados.
3. Abra a cena existente e verifique o Console; registre qualquer erro ou aviso persistente antes de iniciar o próximo marco.
4. No Git, confirme que `Assets/`, `Packages/`, `ProjectSettings/` e os arquivos `.meta` relevantes aparecem como rastreáveis, enquanto `Library/`, `Temp/` e `Logs/` não aparecem.
