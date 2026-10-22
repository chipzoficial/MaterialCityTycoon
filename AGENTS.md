# Material City Tycoon — instruções para agentes de código

## Referências obrigatórias

- Leia `README.md`, `Documentation/Escopo_Tecnico_v0.3.md` e `Documentation/TECH_STACK.md` antes de propor alterações.
- Consulte as versões instaladas no projeto; não assuma versões de Unity, C# ou pacotes.

## Escopo e segurança

- Trabalhe em incrementos pequenos, específicos e verificáveis.
- Não amplie o escopo da tarefa sem autorização explícita.
- Não instale pacotes, atualize dependências ou altere configurações globais do projeto sem autorização.
- Não modifique cenas, prefabs ou arquivos `.meta` de forma destrutiva. Preserve referências, GUIDs e compatibilidade de saves.
- Não crie commits automaticamente nem execute comandos Git destrutivos.

## Arquitetura e testes

- Separe a lógica de domínio da apresentação Unity. Regras de negócio devem ser testáveis sem `MonoBehaviour` sempre que possível.
- Use `ScriptableObject` para definições estáticas editáveis; mantenha estado mutável e dados de salvamento em classes ou DTOs próprios.
- Mantenha uma fonte de verdade para inventário, vendas, caixa e tempo quando esses sistemas forem introduzidos.
- Crie testes para toda lógica testável e informe com precisão o que foi efetivamente testado.

## Entrega de cada tarefa

- Liste os arquivos alterados, as limitações conhecidas e os próximos passos sugeridos.
- Explique os passos manuais necessários no Unity, incluindo o resultado esperado.
- Diferencie verificações executadas de verificações que não puderam ser realizadas. Nunca afirme que uma cena funciona sem testá-la no Editor.
