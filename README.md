# Material City Tycoon

Material City Tycoon é um tycoon de gestão comercial e construção. O objetivo é desenvolver, por incrementos, uma loja de materiais de construção que possa crescer em resposta à demanda da cidade.

## Estado atual

O último marco validado e publicado é o **M5.1 — primeiras vendas e caixa da loja**. O protótipo atual permite construir móveis, configurar seus slots de exposição, transferir mercadorias entre reserva, depósito e exposição, visualizar Argamassa e registrar vendas simuladas.

O trabalho local em andamento inclui a primeira versão de paredes construíveis e a preparação do organizador de miudezas para 25 gavetas. Essas alterações ainda não foram commitadas nem publicadas.

Consulte [CHANGELOG.md](CHANGELOG.md) para o histórico de alterações e o estado das mudanças em desenvolvimento.

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

## Sistemas disponíveis

- Câmera isométrica com movimentação, zoom suave e rotação.
- Grade de construção, preview, ocupação, movimentação, rotação e remoção de móveis.
- Catálogo de quatro móveis: prateleira comercial, expositor de ferramentas, organizador de ferragens e estante industrial.
- Slots de exposição compatíveis com produtos, painel de inspeção e atribuições por instância.
- Reserva, estoque em estantes industriais e estoque exposto, com transferências validadas.
- Necessidades e plano de reposição, além da janela interna `Tools > Material City Tycoon > Store Debug`.
- Visualização de Argamassa em prateleiras comerciais e estantes industriais.
- Vendas simuladas de produtos expostos e caixa por loja.

## Validação manual mínima

1. Abra o projeto no Unity sem solicitar atualização de versão ou de pacotes.
2. Confirme no Package Manager que o Universal RP e o Input System estão instalados.
3. Abra a cena existente e verifique o Console; registre qualquer erro ou aviso persistente antes de iniciar o próximo marco.
4. No Git, confirme que `Assets/`, `Packages/`, `ProjectSettings/` e os arquivos `.meta` relevantes aparecem como rastreáveis, enquanto `Library/`, `Temp/` e `Logs/` não aparecem.

## Versionamento

O repositório usa Git e possui o remoto `origin` configurado para o GitHub. Um commit registra alterações localmente; `git push origin main` publica os commits já existentes na branch `main`. Alterações sem commit não são enviadas.
