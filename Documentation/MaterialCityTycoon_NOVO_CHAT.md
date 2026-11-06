# Mensagem inicial para um novo chat — Material City Tycoon

Vamos retomar o desenvolvimento do **Material City Tycoon**, um tycoon 3D low-poly de loja de materiais de construção, planejado desde 22/10/2026. Estou anexando o arquivo **`MaterialCityTycoon_PROJECT_MASTER.md`**, que deve ser sua referência principal.

**Leia o documento inteiro antes de propor qualquer tarefa.** Diferencie explicitamente (a) decisões vigentes, (b) implementações relatadas, (c) validação direta no Unity e (d) pendências. Não trate testes do Codex como validação visual. Não presuma acesso aos arquivos no meu computador.

**Divisão de responsabilidades:** eu decido as mecânicas e modelo manualmente no Blender; um GPT Blender me orienta; você participa do game design, da arquitetura e da análise técnica; o Codex programa em C# quando eu solicitar. Não peça ao Codex para criar geometria, modelos, meshes, cubos ou placeholders artísticos e não produza prompts para ele automaticamente.

**Estado atual:** estamos na M3, finalizando o Expositor de Sacarias. O palete 1,20×1,00×0,15 m foi integrado ao `PF_PalletBagDisplay`, com footprint lógico 1×1. Sua capacidade desejada é 40 sacos (8 camadas de 5). O FBX individual do cimento 50 kg (0,60×0,40×0,18 m) foi importado na Unity. Ainda precisamos confirmar o prefab visual do cimento e sua associação ao produto, a correção do cálculo A/B (trocar as faixas de 3 e 2; não girar a camada inteira) e o teste em Play Mode. O último relatório foi de 227 testes Edit Mode aprovados **antes da correção geométrica**.

Neste primeiro retorno, apenas:

1. Confirme, de maneira objetiva, que compreendeu a visão do jogo e o estado atual.
2. Identifique as decisões vigentes e as pendências de validação imediata.
3. Não execute tarefas, não gere código e não prepare prompt do Codex até eu pedir.

Quero recuperar a continuidade do nosso projeto e manter uma conversa de planejamento técnico e criativo, não apenas uma sequência de prompts para outro agente.
