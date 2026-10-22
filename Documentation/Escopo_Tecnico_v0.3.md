# MATERIAL CITY TYCOON — ESCOPO TÉCNICO E BRIEFING DE DESENVOLVIMENTO
**Versão:** 0.3 (pré-produção)  
**Data:** 22/10/2026  
**Estado:** conceito definido; implementação ainda não iniciada neste projeto  
**Destinatário:** GPT especializado em Unity, para revisão técnica e orientação de desenvolvimento com Codex

## 0. O que espero da sua ajuda
Quero desenvolver este jogo por etapas com **Unity, C# e Codex no VS Code**. Preciso que você avalie a viabilidade deste escopo, aponte riscos e inconsistências, recomende uma arquitetura sustentável, organize as dependências entre sistemas e me oriente de maneira prática. **Não comece gerando dezenas de scripts.** Primeiro critique a proposta, recomende ajustes e proponha um primeiro marco pequeno e verificável. Depois vamos implementar um marco por vez, usando o Codex para criar e modificar os arquivos e a Unity para configurar cenas, prefabs e testar.

Diferencie o que já foi decidido das sugestões que ainda podem ser revistas. Explique procedimentos na interface da Unity quando forem necessários e não presuma que sei usar Blender.

## 1. Visão de produto
**Nome de trabalho:** Material City Tycoon (provisório).  
**Gênero:** tycoon/simulador de gestão comercial e construção.  
**Inspirações:** SimCity (visão e desenvolvimento urbano), RollerCoaster Tycoon e The Sims (construção/organização), e simuladores de varejo (estoque e reposição). Não pretendemos copiar assets, interface ou mecânicas proprietárias.  
**Plataforma-alvo inicial:** Windows/PC, single-player, offline. **Proposta técnica**, sujeita a confirmação.  
**Estilo artístico decidido:** 3D low-poly, com boa legibilidade a distância, câmera inclinada/isométrica, zoom e rotação.  
**Público/idioma inicial:** português do Brasil, com dados e textos preparados para futura localização.

**Fantasia de jogo:** começar com uma pequena ferragem/depósito de materiais de construção; planejar e construir a loja, adquirir mercadorias, atender demanda local, contratar equipes, organizar entregas e expandir para uma rede de lojas que responde ao crescimento econômico de uma cidade.

**Decisão estrutural:** o jogador é empresário, **não prefeito**. A cidade funciona como um ambiente econômico autônomo, gerando população, obras, consumo e concorrência; não é necessário construir um segundo SimCity completo.

## 2. Dois modos integrados
### 2.1 Visão Cidade
Mapa 3D low-poly com bairros, vias, lotes, edificações, obras, comércio e locais de entrega. O jogador seleciona imóveis, acompanha a demanda, escolhe terrenos e gerencia a localização de estabelecimentos. No futuro: concorrentes, abertura de filiais, frota e rotas.

### 2.2 Visão Loja
Visão detalhada do lote e do estabelecimento: modo construção, exposição de produtos, áreas funcionais, estoque, precificação, mobiliário, atendimento e fluxo operacional. Modo de construção pode pausar o tempo inicialmente (proposta).

**Requisito obrigatório:** a transição entre modos não reinicia o negócio. Dinheiro, estoque, móveis, alterações estruturais, tempo e pedidos pertencem ao estado persistente da simulação, não às cenas visuais. Lojas fora de foco devem continuar **economicamente simuladas**, mas não necessitam de NPCs renderizados nem pathfinding em tempo real.

## 3. Loop principal de jogabilidade
1. Observar demanda e oportunidades na cidade.
2. Comprar/alugar um lote ou reformar um prédio existente (alternativas propostas; definir regras).
3. Construir layout interno/externo, instalar caixas e mobiliário comercial.
4. Negociar e comprar estoque dos fornecedores; receber no depósito.
5. Associar SKUs às posições compatíveis e reabastecer o salão.
6. Simular clientes, compras, pagamentos e redução de estoque de exposição.
7. Resolver rupturas, filas, custos operacionais e entregas; analisar indicadores.
8. Reinvestir em variedade, espaço, equipes, logística e futuras filiais.

**Primeiro teste de diversão:** verificar se organizar, abastecer e reajustar uma pequena loja produz decisões interessantes antes de ampliar o mapa urbano.

## 4. Sistema de construção
**Definido:** construção detalhada por grade, com pisos, paredes, portas, janelas, mobiliário, depósitos, pátios e espaços de circulação.  
**Propostas iniciais:** células de 1 m x 1 m; primeiro lote de 16 m x 20 m; módulos de parede de 1 m; altura padrão aproximada de 3 m. São valores de protótipo e precisam de validação.

Operações: selecionar categoria/objeto; exibir preview/ghost; snap à grade; rotacionar; verificar ocupação e custo; confirmar ou cancelar; mover, desmontar/demolir; salvar e restaurar. Suporte a arrastar múltiplas células para pisos/paredes. Prévia verde/vermelha para posição válida/inválida, com explicação do motivo. Comandos sugeridos: clique para instalar, R para girar, Esc para cancelar, Ctrl+Z para desfazer ações elegíveis (recurso posterior se encarecer o MVP).

**Verificações:** célula ocupada, colisão, interior/exterior, requisitos do piso, parede necessária, acesso para atendimento, limites do lote, rota de entrada/saída, orçamento, capacidade máxima do suporte.

**Definições pendentes:** paredes livres versus módulos de grade; telhados automáticos/ocultáveis; múltiplos andares; regras de energia/iluminação; reforma enquanto a loja opera; terrenos padronizados ou irregulares. Não exigir esses sistemas avançados no protótipo.

Arquitetura sugerida: `GridService`, `PlacementController`, `BuildCatalog`, `PlacementValidator`, `ConstructionCommand`, `BuildingState`, `PlacementPreview`. O validador precisa separar regras geométricas e regras econômicas; o objeto visual não deve ser a fonte dos dados de construção.

## 5. Mobiliário comercial e tipos de exposição
**Divisão principal definida pelo idealizador:**
- **Prateleiras:** produtos apoiados ou empilháveis (tintas, caixas, argamassas).
- **Expositores/ganchos:** ferramentas, lâmpadas, torneiras e embalagens penduradas.
- **Gaveteiros organizadores:** pequenas caixas plásticas frontais em estantes, como a referência enviada; parafusos, porcas, buchas, arruelas e demais miudezas.

**Tipos adicionais propostos:** pallets de chão, racks pesados/porta-pallet, vitrines e balcões fechados, baias de areia/brita, suporte para tubos e perfis, mostruários técnicos. Os três primeiros são os obrigatórios para o primeiro teste.

**Conceito central:** distinguir `FurnitureDefinition` (modelo, categoria, tamanho, preço e capacidade) de `DisplaySlotDefinition` (nível, gancho, gaveta, baia etc.) e de `ProductDefinition` (SKU, embalagem, dimensões, peso, categoria e modos de exposição aceitos). Não criar um script exclusivo para cada produto ou combinação.

Cada instância de mobiliário contém um conjunto de slots independentes. Um slot possui: identificador estável, modo de exposição (`Stack`, `Hang`, `Bin`, `Pallet`, `Bulk`, `Showcase`...), SKU atribuído, quantidade atual, capacidade calculada e configuração de reposição. Um gaveteiro pode ter, por exemplo, 25 bins configuráveis; um painel, múltiplos ganchos; uma prateleira, múltiplos níveis. A capacidade deve considerar pelo menos limites definidos em dados; peso, volume e encaixe geométrico podem ser simplificados no MVP.

**Regras importantes:** uma gaveta pode receber um SKU por padrão; pacotes e unidades soltas precisam de unidade de medida consistente; produtos específicos podem admitir mais de um tipo de exposição. O jogador seleciona um móvel, configura cada slot e transfere quantidade do depósito para a exposição.

**Visualização:** modelos low-poly de móveis e produtos. Pilhas, ganchos e bins apresentam diferentes níveis de preenchimento. Evitar renderizar uma malha por parafuso: usar representações agregadas e LOD/instancing quando necessário. A quantidade numérica é a fonte da verdade; os modelos são uma representação visual dela.

## 6. Catálogo e marcas fictícias
**Estratégia decidida:** marcas criadas para o universo do jogo, com identidade e embalagens próprias; não usar nomes, logotipos nem embalagens originais. Inverter letras não garante segurança jurídica. Nomes a seguir são **provisórios, não verificados quanto a registro ou conflitos**:

| Referência de categoria | Marca fictícia proposta | Linha principal |
|---|---|---|
| Tramontina | Ferravento | ferramentas manuais/jardinagem |
| Zagonel | Lumetra | chuveiros/elétricos |
| Quartzolit | Massentra | argamassas/rejuntes |
| Mor | Campora | utilidades e lazer |
| DeWalt | Torvann | ferramentas elétricas |
| Stanley | Gravik | ferramentas manuais/medição |
| Atlas | Pintara | pincéis/rolos de pintura |
| Tork | Sanivex | higiene profissional |
| Schulz | Aerulon | compressores/pneumática |
| Metasul | Baldiva | baldes/acessórios |
| Krona | Fluxara | tubos/conexões |

Produtos futuros: cimento, tijolos, argamassas, tinta, hidráulica, elétrica, revestimentos, ferragens, ferramentas, madeira e granel. Não criar tudo no MVP.

**Catálogo técnico mínimo por SKU:** `productId` estável, `displayName`, `brandId`, `categoryId`, `sellingUnit`, `unitsPerPurchasePack` (se pertinente), `baseCostCents`, `suggestedPriceCents`, `weight/size` ou `capacityUnits`, `allowedDisplayModes`, `prefab/visualId`, ícone e parâmetros de demanda. Evitar depender do nome visível como chave.

**Unidades de venda:** distinguir unidade, pacote, caixa, saco, metro ou m³ quando aplicável. Para o protótipo, simular parafuso por **unidade inteira** e registrar entrada por pacote convertido em unidades; discutir se haverá vendas fracionadas por peso e materiais a granel depois.

## 7. Estoque e movimentações
Distinguir estoque de depósito, estoque de exposição, estoque reservado e mercadorias em trânsito/entrega (as duas últimas parcelas são futuras). Toda movimentação deve ser explícita: compra/recebimento, transferência depósito→slot, venda, devolução, perda e ajuste de inventário.

Regras obrigatórias: nenhuma quantidade negativa; reposição limitada ao saldo disponível e à capacidade do slot; proibir venda de produto inexistente na exposição (salvo modalidade de encomenda/retirada futura explicitamente implementada); não duplicar produtos ao trocar de cena ou mover móveis; desmontar um móvel deve tratar as quantidades nele contidas de forma definida (devolver ao depósito se houver capacidade, bloquear ou gerar operação apropriada).

**MVP:** reposição pelo painel. Automação e funcionários com tarefas físicas são etapas posteriores.

## 8. Economia, demanda e clientes
**Economia:** caixa financeiro, compras, custo unitário, preço de venda, vendas, custos de construção, lucro bruto/resultado operacional e saldo. Representar dinheiro preferencialmente como inteiros em centavos (`long`), não `float`. Regras de impostos, folha, empréstimos, juros, licenças e sazonalidade ficam para versões futuras.

**Clientes — MVP:** demanda agregada e/ou clientes abstratos em ticks de simulação, influenciada por bairro, categorias, preço relativo, reputação e disponibilidade; não requer NPC com cesta e navegação física. A venda deve ser processada por um **único serviço transacional**, que reduz exposição e aumenta caixa/receita uma única vez.

**Clientes — depois:** NPCs com chegada, navegação, escolha de produtos, fila e pagamento, conectados à mesma API transacional. Não permitir que simulação agregada e NPCs gerem a mesma venda em duplicidade.

**Cidade — MVP:** três bairros com população/demanda parametrizadas, índices de crescimento e algumas obras/eventos simples; sem tráfego real. Demanda alimenta as vendas da primeira loja. Posteriormente: concorrência, contratos com construtoras, localização da loja, rotas logísticas, filiais e centros de distribuição.

**Tempo:** relógio de jogo com ticks fixos e velocidade controlável. Definir depois duração de dia e horários comerciais. A simulação deve ter resultados consistentes mesmo ao alternar de cena; testar com semente aleatória fixa quando possível.

## 9. Funcionários, fornecedores, logística e expansão (pós-MVP)
Funcionários: caixas, vendedores, estoquistas, gerentes, motoristas; contratação, salários, produtividade e atribuições. Fornecedores: catálogo, preço, prazo, lotes e frete. Logística: caminhonetes, caminhões, estoque de carga, rota e cumprimento de entregas. Expansão: terrenos, lojas, custos fixos, especialização por bairro, reputação regional e gestão de múltiplas unidades. Contratos de obras: demanda por lista de materiais, prazos, estoque reservado, multas e reputação. **São objetivos de longo prazo, não requisitos do primeiro build.**

## 10. Arquitetura Unity recomendada para análise
**Stack candidata:** Unity 6 + C# + URP + Input System + UGUI ou UI Toolkit (escolher após avaliação). Usar ferramentas nativas primeiro; não exigir ECS/DOTS, multiplayer, nuvem ou banco SQL no início. Confirmar versões concretas depois de abrir o projeto e revisar compatibilidade de pacotes.

Separação de responsabilidades:
- **Domain/Simulation:** modelos de negócio e regras testáveis sem GameObjects (`Product`, `Inventory`, `Store`, `Sale`, `Demand`, `Clock`).
- **Application/Services:** casos de uso (`BuyStock`, `AssignSlot`, `Restock`, `Sell`, `PlaceFurniture`, `LoadStore`).
- **Presentation/Unity:** cenas, câmera, prefabs, animações, HUD, previews, input e efeitos.
- **Persistence:** snapshots serializáveis, arquivo, versionamento/migração e validação de carga.

Usar `ScriptableObject` para **definições estáticas editáveis** (produto, móvel, marca, balanceamento) e classes C# comuns/DTOs para **estado mutável** (estoques, posições, caixa, hora). `ScriptableObject` não será o mecanismo de salvamento das partidas no build distribuído. IDs estáveis em todos os objetos persistentes. Interfaces e eventos quando agregarem valor, sem excesso de abstração. Considerar assembly definitions apenas se a divisão ficar útil/gerenciável.

Persistência local inicial em JSON versionado, sob `Application.persistentDataPath`, com escrita segura (arquivo temporário, validação e substituição controlada), sem guardar toda a partida em `PlayerPrefs`. Avaliar limitações do `JsonUtility` para dicionários e polimorfismo; utilizar DTOs adequados ou serializador apropriado conforme necessidade. Ter save manual e carga após fechar/reabrir. Preferir sistema de comandos/regras para que salvar e restaurar não dependam de ordem de criação de GameObjects.

Sugestão de cenas:
- `Bootstrap` — inicialização de serviços/sessão.
- `City` — mapa e visão global.
- `Store` — loja detalhada.
- `MainMenu` — opcional no início.

Sugestão de pastas:
```
Assets/_Game/
  Scenes/
  Scripts/
    Core/
    Data/Definitions/
    Domain/
    Services/
    Building/
    Inventory/
    Commerce/
    City/
    Persistence/
    UI/
    Tests/
  Art/Models/
  Art/Materials/
  Art/Textures/
  Prefabs/
  ScriptableObjects/
  UI/
Documentation/
AGENTS.md
```

**Nomes técnicos são propostas**, não uma obrigação para criar todas as pastas e classes antes de existir código que as utilize.

## 11. Pipeline artístico: CorelDRAW → Blender → Unity
O usuário domina mais o CorelDRAW, mas ainda vai aprender Blender.

- Usar CorelDRAW para nomes, logotipos originais, rótulos, etiquetas, ícones e artes de embalagem; exportar PNG com transparência para textura quando necessário.
- Para formas bidimensionais extrudáveis, exportar SVG; no Blender importar curvas, extrudar e converter em malha quando fizer sentido. Isso **não** converte automaticamente qualquer PNG/perspectiva isométrica em modelo 3D completo.
- Modelar no Blender formas básicas de prateleiras, caixas, sacos, tubos, ganchos, gaveteiros e prédios; aplicar UV/texturas onde necessárias.
- Exportar FBX ou outro formato adequadamente suportado, com escala, pivô, orientação e materiais testados na Unity; produzir prefab funcional no editor.
- Para o MVP, usar primitivas e materiais de cor sólida como *greybox*: arte definitiva só depois de provar a mecânica.

Guia visual candidato: formas simples, cores comerciais discretas e diferenciação clara entre materiais/móveis. Manter consistência de escala e pontos de ancoragem. Não construir milhares de meshes independentes para exibir produtos diminutos.

## 12. UI/UX
HUD com data/hora, dinheiro, modo ativo e alertas; painéis de construção (categorias, custo, orientação), móvel (slots, compatibilidade e capacidade), produto (preço, categoria, marca, saldo), compras e vendas, estoque, finanças e mapa/bairros. Tooltips e mensagens de erro úteis: por que um objeto não pode ser colocado, por que um produto não cabe e por que falta estoque. Interface inicial para mouse e teclado; suporte a diferentes resoluções. Design detalhado das telas **a definir**.

## 13. Critérios técnicos não funcionais
- Código e configurações versionados com Git desde o início; `.meta` versionados, pastas temporárias e caches da Unity ignorados.
- Single-player offline; save local confiável; logs de erros; dados centralizados e desacoplados das cenas.
- Escalabilidade arquitetural: comportar centenas de SKUs e muitas prateleiras futuramente, sem construir otimização prematura.
- Desempenho: controlar draw calls, objetos de exibição, atualizações por tick e instâncias ativas; perfil de hardware mínimo ainda indefinido. Evitar `Update()` individual para lógica econômica de cada SKU.
- Testes automatizados de regras (inventário, transações, custo e validação de construção) e testes manuais de integração na Unity.
- Licenciamento de assets de terceiros e originalidade de marcas/embalagens; verificação jurídica antes de distribuição.
- Acessibilidade básica: contraste e tamanho de texto, ícones com rótulos, clareza nas validações.

## 14. MVP / vertical slice inicial
**Não fazer agora:** cidade inteira, veículos reais, IA avançada de NPCs, centenas de produtos, múltiplos andares, multicidade, multiplayer, loja online, física detalhada de materiais ou embalagens finais.

**Cenário mínimo:** um lote plano, uma loja básica; grade; câmera isométrica; três móveis posicionáveis: (1) prateleira com argamassa, (2) painel com martelos pendurados, (3) gaveteiro tipo estante com bins de parafusos. Um depósito de retaguarda. Três SKUs. Capital inicial fictício. Fornecedor simplificado; compras e recebimento imediato; associação dos produtos aos slots; abastecimento manual; vendas abstratas simuladas; atualização de saldo e estoque; transição opcional para um mapa inicial com três bairros; salvar e carregar.

**Critérios de aceite do núcleo:**
1. Posicionar, girar e remover os três modelos, com validação de células e custos.
2. Configurar slots e bloquear exposições incompatíveis.
3. Comprar mercadorias, receber no depósito e repor sem ultrapassar quantidades/capacidades.
4. Registrar vendas sem duplicação, baixando o slot correto e aumentando o caixa.
5. Trocar cidade/loja sem perder dados.
6. Salvar, fechar o jogo, abrir e restaurar posições, estoque, dinheiro e tempo.
7. Não apresentar erros persistentes no Console durante os fluxos principais.

## 15. Marcos de implementação e dependências
**M0 — Base técnica:** projeto Unity/URP, Input System, convenção de pastas, Git, `AGENTS.md`, câmera protótipo, cena básica e testes.  
**M1 — Terreno/câmera/grade:** controles isométricos, limites, grade e raycast do mouse.  
**M2 — Construção mínima:** preview, snap, colisão, rotação, colocar/mover/remover móveis e custo.  
**M3 — Dados dos produtos e slots:** catálogos de 3 SKUs e 3 móveis, regras de compatibilidade e telas de configuração.  
**M4 — Inventário/reabastecimento:** fornecedor simplificado, depósitos, transferências, validações e indicadores visuais.  
**M5 — Vendas/economia:** ticks, cliente abstrato, transação, caixa, preço, demanda e relatórios básicos.  
**M6 — Persistência:** serialização versionada, reconstrução da loja, validação e testes de reabertura.  
**M7 — Cidade simplificada:** cena de mapa com 3 bairros, transição, demanda e simulação fora de cena.  
**M8 — Polimento de vertical slice:** UX, models low-poly piloto, bugs, perfil de desempenho e build Windows.

Cada marco só termina com: projeto compilando, checklist de testes executado, documentação atualizada e commit legível. É permitido ajustar a ordem M6/M7 se a arquitetura justificar, mas a integridade dos dados entre cenas deve ser planejada desde M0.

## 16. Uso intensivo do Codex no VS Code — requisitos do fluxo
**Ferramentas principais:** Unity Editor para cenas/prefabs, VS Code para C#, Codex como agente de programação, Git como histórico/segurança; Blender/CorelDRAW para arte.

**Papel do Codex:** criar/editar scripts, DTOs, ScriptableObjects, testes, ferramentas de editor, documentação, revisões, correções e refatorações pequenas. Não presumir que o agente pode clicar no Unity Editor ou validar visualmente uma cena; isso dependerá das ferramentas de automação efetivamente disponíveis. A pessoa deve executar o teste no editor, trazer logs/imagens e solicitar correção contextualizada.

**Regras para o Codex:**
1. Ler `README.md`, o escopo e `AGENTS.md` antes de propor mudanças.
2. Implementar **uma tarefa pequena por solicitação**; apresentar arquivos afetados e o comportamento esperado.
3. Reutilizar a arquitetura existente em vez de criar novos `Manager` ou singletons indiscriminadamente.
4. Não instalar pacotes nem mudar versão/configuração global do projeto sem justificar e pedir autorização.
5. Não editar assets binários, cenas/prefabs serializados ou arquivos `.meta` de maneira destrutiva; preferir instruções claras de configuração no Unity ou scripts Editor seguros quando aprovados.
6. Criar testes Edit Mode de domínio; fornecer roteiro de teste manual Play Mode e explicar como verificar resultado.
7. Informar o que foi **efetivamente** testado e o que não pôde ser testado; nunca afirmar que uma cena funciona sem rodá-la.
8. Não usar APIs obsoletas; consultar as versões/pacotes **instalados** no projeto.
9. Preservar referências de assets, GUIDs e compatibilidade de saves ao refatorar.
10. Antes de encerrar: resumo, arquivos alterados, instruções na Unity, testes, limitações e próxima tarefa.

**Arquivo `AGENTS.md` inicial sugerido:**
```
# Material City Tycoon — instruções para agentes de código
- Projeto Unity 6 / C# / URP; versões exatas em Documentation/TECH_STACK.md.
- Ler Documentation/GDD.md e Documentation/ARCHITECTURE.md.
- Fazer mudanças pequenas, específicas e testáveis; não ampliar escopo.
- Separar definições ScriptableObject de estados mutáveis/salvamentos.
- Lógica de negócio testável sem MonoBehaviour sempre que possível.
- Uma fonte da verdade para inventário, vendas, caixa e tempo.
- Não criar dependências/pacotes ou alterar ProjectSettings sem autorização.
- Não modificar cenas/prefabs/arquivos .meta manualmente sem segurança.
- A cada tarefa: listar alterações, limitações e roteiro de teste no Editor.
- Nunca afirmar que testou no Unity se não executou teste real.
```

**Modelo de tarefa para Codex:**
```
Contexto: Material City Tycoon, Unity [versão real do projeto].
Marco atual: M[x].
Objetivo único: [comportamento concreto].
Arquivos/sistemas existentes: [informar].
Requisitos: [3-6 itens verificáveis].
Não fazer: [itens fora de escopo].
Critérios de aceite: [ação no Editor + resultado esperado].
Antes de editar: inspecione a estrutura atual, proponha o menor conjunto de mudanças.
Depois: implemente, escreva testes quando viável e descreva passos manuais na Unity.
```

**Fluxo de interação recomendado:** conversar com o GPT Unity para arquitetura e revisão de planos; solicitar um prompt circunscrito ao Codex; executar no VS Code; testar no Unity; devolver erros/prints ao GPT e/ou Codex; registrar resultado e commit. Evitar delegar um jogo inteiro de uma vez.

## 17. Decisões ainda pendentes (não inventar como resolvidas)
**Alta prioridade:** (a) versão exata de Unity e pacotes, (b) UI Toolkit versus UGUI, (c) tamanho e granularidade da grade, (d) mecanismo de demanda/venda inicial, (e) política de unidades de parafusos e materiais a granel, (f) regras mínimas de capacidade/compatibilidade, (g) política ao remover mobiliário com estoque, (h) estratégia de save e troca de cenas, (i) custo inicial e limites de construção.

**Média prioridade:** controles definitivos; horários comerciais; IA visual de clientes; filas; funcionários; fornecedores com lead time; competição; contratos; entregas; marcas e linhas de qualidade; armazenamento de múltiplas filiais; sistemas de telhados e pisos superiores.

**Posterior:** arte final, marketing, publicação, tradução, achievements, suporte a mods, otimização para hardware definido, acessibilidade avançada.

## 18. Principais riscos para avaliação
- **Escopo excessivo:** dois modos e simulação de comércio/cidade podem virar dois jogos; manter cidade como motor de demanda primeiro.
- **Acoplamento:** se estoque/venda estiverem nos GameObjects da prateleira, a troca de cena quebrará a simulação; centralizar domínio/estado.
- **Explosão de assets:** centenas de produtos e marcas antes do MVP atrasariam o jogo; começar com três SKUs e modelos de prova.
- **Desempenho:** muitas instâncias de miudezas e NPCs; usar visual agregado e simulação por ticks.
- **Economia sem diversão:** planilhas complexas sem decisões relevantes; testar cedo o ciclo construir→abastecer→vender→expandir.
- **Persistência:** objetos removidos/movidos e saves antigos exigem IDs e versionamento.
- **Marcas:** nomes e embalagens fictícios precisam de originalidade e checagem antes da comercialização.
- **Codex:** código gerado pode compilar mas não estar corretamente conectado a cenas/prefabs; validar cada etapa no Unity Editor.

## 19. O que quero que você, GPT especializado em Unity, responda primeiro
1. Avalie a viabilidade para um desenvolvedor independente que pretende aprender enquanto constrói, usando intensivamente Codex.
2. Identifique o que é necessário simplificar, adiar ou reformular tecnicamente; não concorde automaticamente com todas as ideias.
3. Recomende a arquitetura mínima viável para domínio, cenas, construção, mobiliário, inventário e persistência; justifique escolhas que afetem muito o futuro.
4. Revise a sequência de marcos e identifique dependências técnicas mal posicionadas.
5. Diga **exatamente por onde começar**, incluindo setup do Unity, Git e VS Code/Codex, sem gerar o jogo todo de uma vez.
6. Escreva o **primeiro prompt pronto para o Codex**, limitado a M0, e um checklist de testes executáveis por iniciante.
7. Informe quais decisões precisam ser tomadas agora e quais podem permanecer abertas sem impedir o primeiro protótipo.

**Importante:** o conceito e as definições deste documento nasceram de planejamento, não de um projeto já implementado. Se precisar de arquivos do projeto para avaliar compatibilidade, solicite os arquivos/versões concretos no momento adequado. Não assuma que este projeto está criado ou que os scripts já existem.
