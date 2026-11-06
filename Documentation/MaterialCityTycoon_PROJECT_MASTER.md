# MATERIAL CITY TYCOON
## Documento Mestre do Projeto — visão, decisões, implementação, pendências e protocolo de continuidade

**Versão do documento:** 1.0  
**Data-base:** 05/11/2026  
**Situação:** protótipo em desenvolvimento; foco atual na integração visual de produtos da M3  
**Nome do jogo:** *Material City Tycoon* (provisório)  
**Propósito:** referência central para decisões, continuidade entre conversas e orientação de desenvolvimento com Unity, VS Code e Codex.

> **Nota de confiabilidade:** este documento consolida GDDs, documentos de transição, relatórios apresentados pelo desenvolvedor e suas confirmações em conversa. Não resulta de inspeção direta da cópia local do projeto ou execução própria do Unity. Quando uma afirmação depende de relatório do Codex ou ainda não teve confirmação no Play Mode do projeto principal, isso está explicitado.

---

# 1. Como interpretar o estado do projeto

Os termos abaixo são obrigatórios em futuras atualizações:

| Status | Significado |
|---|---|
| **DECIDIDO** | Regra de design ou restrição aprovada pelo desenvolvedor. Não significa código implementado. |
| **IMPLEMENTADO (relatado)** | Um relatório do Codex, documentos técnicos ou o desenvolvedor indicam que houve implementação. Pode carecer de validação no projeto principal. |
| **VALIDADO PELO DESENVOLVEDOR** | O desenvolvedor confirmou comportamento ou resultado na Unity/Blender. Informar exatamente o que ele confirmou. |
| **TESTADO (relatado)** | Houve relato de compilação ou teste automatizado, indicando quantidade e ambiente; não equivale a validação visual. |
| **PENDENTE** | Não concluído, não confirmado ou dependente de trabalho futuro. |
| **PROPOSTO** | Ideia ainda não aprovada como escopo definitivo. |

**Regra temporal:** uma decisão mais recente e explicitamente aprovada substitui a anterior. Registrar a mudança e nunca misturar os valores antigos com os vigentes. Caso o código em disco divirja do documento, identificar a divergência e solicitar/inspecionar evidência antes de qualquer alteração destrutiva.

**Regra de honestidade:** não declarar que um comando Editor foi executado, que um prefab foi associado, que um teste passou no projeto principal ou que uma cena funcionou no Play Mode sem confirmação efetiva.

---

# 2. Visão original e identidade do jogo

## 2.1. Conceito — Modelo C

Simulador 3D low-poly de **construção, gestão empresarial e expansão de lojas de materiais de construção**, combinando duas escalas conectadas:

1. **Visão Loja:** construir, reformar, equipar e organizar ambientes; administrar depósito, prateleiras, produtos, compras, reposição, atendimento e vendas.
2. **Visão Cidade:** acompanhar bairros, obras, terrenos, demanda, concorrência e oportunidades comerciais; mais adiante abrir filiais e gerir logística.

**Decisão fundamental:** o jogador é **empresário, não prefeito**. A cidade evolui por simulação autônoma e gera demanda comercial. Não construir um segundo *SimCity* como pré-requisito para o núcleo do jogo.

**Fantasia central:** começar com uma ferragem de bairro, crescer até um home center e, posteriormente, administrar uma rede regional.

## 2.2. Referências e pilares

Referências conceituais: *SimCity* (economia urbana), *RollerCoaster Tycoon* (gestão e expansão), *The Sims* (construção e organização) e simuladores de varejo (estoque/atendimento). São referências de experiência, não autorização para copiar recursos.

Pilares:

- Construção física detalhada e legível.
- Administração de preço, compras, caixa, margem, estoque e equipe.
- Reação à demanda da cidade, obras e bairros.
- Progressão de uma loja simples até múltiplas unidades, com logística.
- Interface clara e automação gradual para evitar microgerenciamento sem propósito.

O **ciclo central desejado** é: **construir/organizar → comprar → receber → armazenar → abastecer → vender → registrar resultado → reinvestir/expandir**. A parte econômica/comercial final desse ciclo **ainda não foi implementada integralmente**.

## 2.3. Direção de produto

- Nome **provisório**: *Material City Tycoon*. Outros conceitos de nome, como *Bricks n’ Bolts* e *Ferragem & Cia*, foram explorados, sem substituir o nome de trabalho.
- Gênero: tycoon / simulação empresarial / construção.
- Plataforma inicial planejada: Windows/PC, single-player, offline.
- Idioma inicial: português do Brasil; preparar dados/textos pensando em localização futura.
- Direção visual: 3D low-poly estilizado, câmera ortográfica inclinada/isométrica, zoom, panorâmica e rotação.
- Paleta original *sugerida*: areia, concreto, terracota, azul-petróleo, verde suave e amarelo de sinalização. Não é um padrão cromático irrevogável.

## 2.4. Cidade: visão de longo prazo, não implementação confirmada

O GDD considera bairros residenciais em expansão, consolidados e mistos; demanda por categorias distinta, obras temporárias, concorrência, compra de terrenos e futuras filiais. O ideal é simular economicamente lojas fora de foco sem renderizar NPCs nem fazer pathfinding completo. Cidade e loja devem compartilhar dados financeiros e estoques persistentes, sem duplicar transações.

**Não há confirmação de implementação funcional da Visão Cidade, transição Cidade/Loja ou save/load definitivo no estado atual.**

---

# 3. História do planejamento e marcos

| Período | Evolução |
|---|---|
| **22/10/2026** | Planejamento do conceito híbrido Cidade+Loja; GDD v0.1, consolidação v0.2 e escopo técnico v0.3. A documentação ainda descrevia pré-produção. |
| **23/10/2026** | Desenvolvedor informou base funcional de criação, movimentação, rotação e exclusão de móveis, além de controles da câmera. Consolidado catálogo criativo de marcas v0.4. |
| **Fase M0–M1** | Grid, câmera, posicionamento; evolução de definições de móveis/produtos, slots, reserva, estoque e exposição. |
| **Fase M2** | Paredes ortogonais e diagonais, portas, pisos; correções de projeção do mouse e remoção em Edit/Play Mode; acabamento de junções adiado. |
| **Fase M3a** | Quatro SKUs novos: cimento, tinta, trena e buchas. Generalização de operações de estoque/exposição. |
| **Fase M3b** | Dois móveis novos: Expositor de Sacarias e Gôndola Central Dupla. Surgiram visuais provisórios criados em Unity; posteriormente proibida nova modelagem artística por Codex sem autorização. |
| **Integração artística** | Modelagem manual do palete de madeira, importação FBX, correção de luz/câmera importadas, integração ao prefab, footprint alterado de 2×2 para 1×1. |
| **Empilhamento M3b.1** | Capacidade alterada de 24 para 40; algoritmo de 8×5 e presenter implementados, mas padrão B interpretado incorretamente como rotação global. Correção especificada; execução não confirmada. |
| **05/11/2026** | Desenvolvedor confirmou que o FBX individual do saco de cimento foi importado corretamente na Unity. Criação do prefab visual e vínculo ao produto ainda não confirmados. |

Os documentos de 22/10 registravam intenção/escopo, não funcionalidades já programadas naquele dia.

---

# 4. Ambiente de desenvolvimento e estrutura de diretórios

- **Unity:** 6000.6.3f1 (Unity 6.6).
- **Render Pipeline:** URP.
- **Input:** New Input System.
- **Programação:** C#, VS Code, Codex/Copilot.
- **Arte:** Blender 5.2.3 LTS e CorelDRAW (embalagens, rótulos, elementos gráficos).
- **Versionamento:** Git, preservar `.meta`, GUIDs e referências serializadas.
- **Diretório informado mais recentemente:** `E:\UnityGames\MaterialCityTycoon`.
- **Cena principal informada:** `MaterialCityTycoon\Assets\_Game\Scenes\Store.unity`.

Estrutura lógica relevante:

```text
MaterialCityTycoon/
└─ Assets/
   └─ _Game/
      ├─ Art/
      │  ├─ Models/
      │  ├─ Materials/
      │  └─ Textures/
      ├─ Editor/
      │  └─ Tests/
      ├─ Prefabs/
      │  ├─ Building/
      │  ├─ Storage/
      │  └─ Products/        ← pasta criada/sugerida na integração do cimento; confirmar no disco
      ├─ Scenes/
      ├─ Scripts/
      │  └─ Building/
      └─ ScriptableObjects/
         ├─ Furniture/
         └─ Products/
```

**Não inventar pastas:** caminhos anteriores de relatórios podem ter divergências de grafia (`Assets_Game` versus `Assets\_Game`) e raízes antigas. Usar a estrutura real do repositório ao executar tarefas. Nas respostas, preferir caminho relativo começando por `MaterialCityTycoon\`.

**Padrões de nomes adotados:** `MCT_` modelos, `PF_` prefabs, `DEF_` definições, `MAT_` materiais, `TEX_` texturas, IDs lógicos em `snake_case` e estáveis. Não renomear IDs, `.meta` ou classes serializadas sem migração específica.

---

# 5. Acordo de responsabilidades — obrigatório

| Participante | Responsabilidade |
|---|---|
| **Desenvolvedor / idealizador** | Decide direção criativa, gameplay, prioridades e escopo; **modela pessoalmente** os objetos 3D no Blender; valida no Unity e aprova resultados. |
| **Assistente principal (este papel)** | Planejamento, arquitetura, crítica de game design, análise técnica, continuidade histórica e preparação de instruções para Codex somente quando solicitadas. Não substituir pensamento de design por produção incessante de prompts. |
| **Codex/Copilot** | Código C#, integração de modelos existentes, componentes, dados, testes, ajustes técnicos e ferramentas Editor aprovadas. Não criar modelos artísticos/procedurais ou placeholders de móveis/produtos sem autorização explícita. |
| **GPT especialista em Blender** | **Instrutor**: orientar manualmente o desenvolvedor, passo a passo, em ferramentas, geometria, UV, materiais e FBX. Não modelar nem gerar Python por conta própria. |

**Restrições permanentes:**

1. Não repetir perguntas já respondidas nem reabrir decisões consolidadas sem razão técnica.
2. Não presumir que houve execução de prompt, associação de asset ou teste visual.
3. Não criar geometrias substitutas com `GameObject.CreatePrimitive`, meshes procedurais, cubos ou modelos artísticos via Codex.
4. Integrar os FBX feitos pelo desenvolvedor **preservando os prefabs funcionais**, componentes, GUIDs, IDs, slots e estoque.
5. Não executar comandos Editor que possam reconfigurar/sobrescrever modelos personalizados sem avaliar efeitos.
6. Não promover uma pequena correção a auditoria/refatoração extensa.
7. Diferenciar fato, hipótese e sugestão; diagnosticar a partir de evidência.
8. Evitar uso desnecessário de Codex/quota. Responder perguntas de planejamento sem automaticamente produzir prompt.
9. Comunicar-se em **português brasileiro padrão**, sem dialeto gaúcho.
10. Não avançar o roadmap sem discutir impacto na experiência de jogo.

---

# 6. Sistemas implementados / relatados

## 6.1. Construção e câmera

| Recurso | Situação e observações |
|---|---|
| Câmera isométrica, panorâmica, zoom, rotação | **IMPLEMENTADO (relatado/confirmado pelo desenvolvedor)**. |
| Grid de 1×1 metro | **IMPLEMENTADO (relatado)**. |
| Criar/mover/rotacionar/remover móveis | **VALIDADO PELO DESENVOLVEDOR** desde 23/10. |
| Preview verde/vermelho e validação de ocupação/acesso | **IMPLEMENTADO (relatado)**. |
| Paredes ortogonais | **IMPLEMENTADO (relatado)**; 3,00 m altura, 0,15 m espessura, módulos de 1 m. |
| Diagonais a 45° | **IMPLEMENTADO (relatado)**; uma ferramenta de parede, ângulo calculado pela direção do arraste, segmentos com comprimento geométrico conforme endpoints. |
| Portas internas/externas | **IMPLEMENTADO (relatado)** para paredes ortogonais; vão de 1,00 m × 2,10 m, verga 0,90 m, passagem lógica liberada. |
| Pisos | **VALIDADO PELO DESENVOLVEDOR**; clicar célula, arrastar retângulo, Shift+arraste remover área, Delete remover célula. |
| Junções reta/diagonal | **PENDENTE** refinamento visual, deliberadamente adiado. |

**Bugs relevantes corrigidos (relatados):**

- Raycast do mouse atingia a face lateral do `Ground` e gerava altura errada em paredes perto da borda. Passou a projetar a posição no plano horizontal do chão.
- Diferenciação de `Destroy` em Play Mode e `DestroyImmediate` em Edit Mode na remoção de objetos de construção.

Não reintroduzir esses bugs ao alterar o sistema de construção.

## 6.2. Estoque, móveis e exposição

Estrutura existente relatada:

- `ProductDefinition` para características estáticas do SKU.
- `FurnitureDefinition` para footprint, compatibilidades, slots, capacidades e acessos.
- Slots independentes com modos `Stack`, `Hang`, `Bin`.
- Estoque de reserva/depósito separado do estoque exposto.
- Associação de produto ao slot e transferência reserva → exposição.
- Quantidade lógica como fonte da verdade; visuais refletem quantidade.
- Inspeção de móveis, vendas simuladas e `StoreDebugWindow`/ferramentas de debug.

**PENDENTE validar completamente no Play Mode:** controle independente de alguns slots no expositor de ferramentas, das 25 gavetas no organizador e dos móveis mais recentes, bem como o comportamento dos modelos definitivos. **Não confundir vendas simuladas com economia/caixa/cliente final.**

---

# 7. Catálogo atual de móveis

| ID técnico | Prefab / Definition | Footprint | Slots / detalhes | Estado |
|---|---|---:|---|---|
| `commercial_shelf` | `PF_CommercialShelf` / `DEF_CommercialShelf` | 1×2 | `shelf_bottom`, `shelf_middle`, `shelf_top`; `Stack`; capacidade inicial de 12 sacos de argamassa por nível | **IMPLEMENTADO (relatado)** |
| `tool_display` | `PF_ToolDisplay` / `DEF_ToolDisplay` | conforme Definition existente | `Hang`, acesso por faces; exposição de ferramentas e trena | **IMPLEMENTADO (relatado)**; independência de posições a confirmar |
| `parts_bin_rack` | `PF_PartsBinRack` / `DEF_PartsBinRack` | 2×1 | 25 compartimentos (5×5), de `bin_r01_c01` a `bin_r05_c05`; acesso frontal; `Bin` | **IMPLEMENTADO (relatado)**; validação independente das gavetas pendente |
| `industrial_storage_shelf` | `PF_IndustrialStorageShelf` / `DEF_IndustrialStorageShelf` | 2×1 | níveis de depósito: 24/24/18 sacos de argamassa (total 66); acesso frente/trás | **IMPLEMENTADO (relatado)**; modelo Blender e ajuste de altura desejado de 2,40 m a confirmar |
| `pallet_bag_display` | `PF_PalletBagDisplay` / `DEF_PalletBagDisplay` | **1×1 (decisão atual)** | `pallet_bags`, `Stack`, **40 sacos (decisão atual)**; acessos frente/trás; argamassa/cimento | Prefab funcional com palete manual integrado; layout de sacos ainda requer correção/validação |
| `double_sided_gondola` | `PF_DoubleSidedGondola` / `DEF_DoubleSidedGondola` | 2×1 | 4 slots `Stack` frente/trás superior/inferior; capacidade de 4 unidades por slot | **IMPLEMENTADO (relatado)**; visual definitivo Blender pendente |

Caminhos conhecidos:

- `MaterialCityTycoon\Assets\_Game\Prefabs\Building\PF_CommercialShelf.prefab`
- `MaterialCityTycoon\Assets\_Game\Prefabs\Building\PF_PalletBagDisplay.prefab`
- `MaterialCityTycoon\Assets\_Game\Prefabs\Building\PF_DoubleSidedGondola.prefab`
- `MaterialCityTycoon\Assets\_Game\Prefabs\Storage\PF_IndustrialStorageShelf.prefab`
- `MaterialCityTycoon\Assets\_Game\ScriptableObjects\Furniture\DEF_PalletBagDisplay.asset`

**Não recalcular capacidades de outros móveis sem aprovação**, especialmente a estante de argamassa. Cimento não foi automaticamente liberado para a estante industrial só porque ambos são produtos empilháveis.

---

# 8. Catálogo atual de produtos

| Produto | ID conhecido | Modo | Preço de cadastro no protótipo |
|---|---|---|---:|
| Argamassa | `argamassa` | Stack | Não recuperado aqui |
| Martelo | `martelo` | Hang | Não recuperado aqui |
| Parafusos | `parafusos` | Bin | Não recuperado aqui |
| Cimento 50 kg | `cimento_50kg` | Stack | 3.500 centavos / R$ 35,00 |
| Tinta acrílica 18 L | `tinta_acrilica_18l` | Stack | 15.990 centavos / R$ 159,90 |
| Trena 5 m | `trena_5m` | Hang | 2.990 centavos / R$ 29,90 |
| Buchas plásticas nº 8 | `buchas_plasticas_n8` | Bin | 890 centavos / R$ 8,90 |

Os quatro últimos entraram na **M3a**. Os preços acima são valores existentes no protótipo relatado, não decisões finais de balanceamento.

O GDD v0.1 também previa dez categorias/produtos futuros (areia, brita, tijolo, tubo PVC, joelho PVC, cabo elétrico, telha etc.); **não contabilizá-los como implementados**.

---

# 9. Arte, modelos, marcas e pipeline

## 9.1. Pipeline artístico obrigatório

**CorelDRAW (quando útil) → Blender manual → FBX/textura → importação Unity → prefab visual → associação ao ProductDefinition/FurnitureDefinition → validação na cena.**

O desenvolvedor faz a modelagem manual. O GPT Blender é instrutor. Não confundir modelo FBX com prefab funcional, nem reconstruir o prefab funcional a partir do FBX.

Estilo low-poly com boa leitura na câmera; usar arte original e dimensões/pivôs consistentes. Um item muito pequeno visto de longe não precisa do mesmo nível de geometria de um móvel grande. Texturas/UV apropriadas quando houver embalagens e rótulos.

## 9.2. Modelos e estados recuperados

| Asset | Estado que podemos afirmar |
|---|---|
| `MCT_Expositor_Sacarias.fbx` | **Modelado pelo desenvolvedor, importado e integrado ao prefab funcional**. |
| Modelo do organizador de miudezas | Criado no Blender, importado; matriz de 25 bins associada funcionalmente. |
| Estante industrial de depósito | Modelo Blender desenvolvido; confirmar ajuste final de 2,40 m e integração final. |
| `MCT_Saco_Cimento_50kg.fbx` | **Importação confirmada pelo desenvolvedor em 05/11**; prefab e referência visual não confirmados. |
| `MCT_Gondola_Central_Dupla` | Modelo definitivo ainda pendente de confirmação; prefab funcional usa visual provisório. |
| `MCT_Lata_Tinta_Acrilica_18L` | Modelo definitivo não confirmado. |
| `MCT_Trena_5m` | Modelo definitivo não confirmado. |
| `MCT_Embalagem_Buchas_N8` | Modelo definitivo não confirmado. |
| Saco de argamassa | Modelo visual existente e utilizado em trabalho anterior. |

## 9.3. Marcas fictícias — decisões e cautelas

Decidiu-se criar identidades originais, sem copiar logos, embalagens ou identidade visual de marcas reais. O catálogo criativo **v0.4 (23/10/2026)** reuniu 27 marcas provisórias, com status de verificação jurídica **pendente**:

| ID | Marca | Segmento | ID | Marca | Segmento |
|---|---|---|---|---|---|
| B001 | Ferravento | Ferramentas manuais | B015 | Fixaoris | Fixadores |
| B002 | Duvorena | Chuveiros/elétrica | B016 | Fiozenta | Fios/cabos |
| B003 | Massentra | Argamassa | B017 | Brilhevora | Iluminação |
| B004 | Lazufora | Utilidades/lazer | B018 | Madevaris | Madeira |
| B005 | Trevakor | Ferramentas elétricas | B019 | Fechorna | Fechaduras |
| B006 | Prumekra | Medição | B020 | Telhavox | Telhas |
| B007 | Pincevora | Pintura | B021 | Reservora | Reservatórios |
| B008 | Higivaro | Higiene | B022 | Vidraceno | Vidros/esquadrias |
| B009 | Pnevoris | Pneumática | B023 | Episento | EPIs |
| B010 | Baldiva | Baldes/organização | B024 | Jardilora | Jardinagem |
| B011 | Tubelora | Tubos/conexões | B025 | Abralvora | Abrasivos |
| B012 | Cimentorva | Cimento | B026 | Portelora | Portas/janelas |
| B013 | Cromavora | Tintas | B027 | Metaltriva | Metais sanitários |
| B014 | Ceramovia | Revestimentos |  |  |  |

Nomes anteriores descartados/substituídos editorialmente incluem: Lumetra→Duvorena, Campora→Lazufora, Torvann→Trevakor, Gravik→Prumekra, Pintara→Pincevora, Sanivex→Higivaro, Aerulon→Pnevoris, Fluxara→Tubelora. **Não tratar quaisquer nomes como marcas disponíveis/registradas**; pesquisa específica no INPI e avaliação de semelhanças ficam pendentes antes de publicação. A incorporação do catálogo criativo aos dados de runtime também não foi confirmada.

## 9.4. Áudio

Foram discutidos prompts de música instrumental calma para o modo construção, incluindo jazz/sax discreto, retrô, orquestração leve e referências de simuladores. **Não há trilha final integrada confirmada.**

---

# 10. Expositor de Sacarias — especificação vigente (prioridade imediata)

## 10.1. Evolução de decisões

| Parâmetro | M3b original (obsoleto) | Decisão atual |
|---|---:|---:|
| Footprint lógico | 2×2 | **1×1** |
| Capacidade | 24 sacos | **40 sacos** |
| Camadas | Não fixadas nessa primeira configuração | **8** |
| Sacos por camada | Não fixados nessa primeira configuração | **5** |
| Modelo físico | Geometria provisória | **FBX manual de madeira** |
| Acessos | Frente/trás, offsets antigos | **Frente `(0,-1)` e trás `(0,1)`** (validados pelo preview verde) |

**Não restaurar a configuração antiga de 24 unidades/2×2.**

## 10.2. Palete físico

- Nome: `MCT_Expositor_Sacarias.fbx`.
- Madeira low-poly, tábuas, blocos e longarinas.
- Dimensões: **X 1,20 m × Z 1,00 m × Y 0,15 m**, conforme orientação adotada na Unity.
- Pivô: centro da base.
- Topo da superfície: **Y = 0,15 m**.
- Footprint lógico do móvel: **1×1**.
- Visual integrado ao `PF_PalletBagDisplay`, preservando scripts e slots existentes.

**Risco aberto:** modelo de 1,20 m excede a célula lógica de 1,00 m em um eixo; verificar interferência entre vizinhos antes de fixar regras finais de ocupação. Não redimensionar modelo/footprint sem autorização.

**Incidente de importação resolvido:** a Unity trouxe objetos Camera e Light do Blender, alterando a iluminação da cena. O desenvolvedor identificou e removeu esses elementos; futuras importações devem desativar luzes/câmeras. `CellIndicator` é um objeto preexistente e separado do FBX, não a causa comprovada desse problema.

## 10.3. Saco individual de cimento

- Modelo: `MCT_Saco_Cimento_50kg.fbx`.
- Dimensões de referência: **0,60 × 0,40 × 0,18 m** (comprimento × largura × altura quando deitado).
- Preferência de pivô: centro na base; na Unity, comprimento em X, largura em Z, altura em Y.
- Visual low-poly de saco cheio com ligeiro volume e dobras, embalagem fictícia; modelagem feita manualmente pelo desenvolvedor.
- **Importação do FBX na Unity: confirmada em 05/11/2026.**
- Pasta orientada: `MaterialCityTycoon\Assets\_Game\Art\Models\Products\`.
- **Não há confirmação** de criação de `PF_Cimento50kg` em `Prefabs\Products`, nem associação do prefab visual ao asset de `ProductDefinition` de `cimento_50kg`. Verificar campo e nome reais no Inspector antes de alterar.

## 10.4. Layout físico A/B — decisão correta

Capacidade lógica **40**; exatamente **8 camadas de 5 sacos**; alternância **A/B/A/B/A/B/A/B**. Cada camada contém duas faixas perpendiculares: três sacos ocupando 1,20 × 0,60 m e dois sacos ocupando 1,20 × 0,40 m. O conjunto ocupa **1,20 × 1,00 m**.

**Camada A:** primeiro faixa de 3, depois faixa de 2.  
**Camada B:** primeiro faixa de 2, depois faixa de 3.

**A camada B NÃO é a rotação global de 90° da camada A.** Inverter apenas a posição das duas faixas, mantendo a orientação individual dos sacos. Girar toda a camada produziria 1,00 × 1,20 m e ultrapassaria a profundidade do palete.

Coordenadas de referência, relativas ao centro local do palete (X,Z):

| Camada | Faixa | X dos centros (m) | Z dos centros (m) | Comprimento do saco |
|---|---|---|---|---|
| A | 3 sacos | −0,40; 0; +0,40 | −0,20 | Longitudinal em Z |
| A | 2 sacos | −0,30; +0,30 | +0,30 | Longitudinal em X |
| B | 2 sacos | −0,30; +0,30 | −0,30 | Longitudinal em X |
| B | 3 sacos | −0,40; 0; +0,40 | +0,20 | Longitudinal em Z |

Para o FBX com 0,60 m longitudinal em X, a faixa de 3 usa giro Y de 90° (conferir sentido local), e a faixa de 2, 0°; utilizar offsets parametrizados pelas dimensões se possível.

- Primeira camada apoiada em `Y = 0,15 m` quando o pivô do saco é na base.
- Incremento nominal por camada: `0,18 m`.
- Altura total nominal: `0,15 + 8 × 0,18 = 1,59 m`.
- **Visual = estoque:** 0 sem saco, 1 um saco, 3 três, 5 uma camada, 7 uma camada+2, 20 quatro camadas, 39 sete camadas+4, 40 oito camadas.
- Remoção de cima para baixo, preservando camadas inferiores.
- Posições/rotações **locais** para seguir o palete ao mover e rotacionar.
- Cada saco uma instância de prefab visual fornecido; sem fabricar mesh substituto; sem collider individual por padrão.

## 10.5. Implementação já relatada, mas não concluída visualmente

O Codex relatou alterações em oito arquivos, destacando:

- `MaterialCityTycoon\Assets\_Game\Scripts\Building\FurnitureDefinition.cs`: configuração serializável do layout.
- `MaterialCityTycoon\Assets\_Game\Scripts\Building\PalletBagDisplayLayout.cs`: cálculo 8×5.
- `MaterialCityTycoon\Assets\_Game\Scripts\Building\DisplaySlotProductVisualPresenter.cs`: aplicação de posições/rotações locais.
- `MaterialCityTycoon\Assets\_Game\Editor\ConfigureDisplayFurnitureMenu.cs`: menu específico.
- `MaterialCityTycoon\Assets\_Game\Editor\Tests\DisplayFurnitureTests.cs`.
- `MaterialCityTycoon\Assets\_Game\Editor\Tests\PalletBagDisplayLayoutTests.cs`.

Menu adicionado: **`Tools > Material City Tycoon > Configure Pallet Bag Display`**. Deve configurar 1×1, 40 unidades, superfície 1,20 × 1,00 m em Y 0,15 m, espaço 0 e acessos frente/trás, sem reconstruir o prefab manual.

**Problema confirmado pelo relatório:** algoritmo inicialmente girava a camada inteira, resultando em 1,00 × 1,20 m; recusava criar visual incorreto, mantendo inventário lógico intacto. **Foi elaborado um prompt de correção para a alternância por faixas, mas ainda não houve retorno confirmando sua aplicação.**

**Não informar “empilhamento corrigido” ou “40 sacos validados” antes de evidência.** O menu específico existe no código segundo relatório; a execução efetiva no projeto principal também não foi confirmada.

---

# 11. Ferramentas Editor, testes e riscos técnicos

## 11.1. Comandos de interesse

- `Tools > Material City Tycoon > Configure Display Furniture`: **cautela**; versões anteriores podiam regenerar configurações ou visuais provisórios de móveis. Não rodar sem avaliar risco ao prefab personalizado.
- `Tools > Material City Tycoon > Configure Pallet Bag Display`: novo comando específico relatado; preservar geometria do palete e sua hierarquia.
- `Tools > Material City Tycoon > Repair Portuguese Text Encoding`: reparo para strings/representações em português após correção do mojibake. Verificar se executado no projeto principal; não assumir.

## 11.2. Mojibake/UTF-8

Foram relatadas strings corrompidas para “Gôndola Central”, “Tinta acrílica”, “Buchas plásticas” e rótulos. Corrigidas strings de geração, `.editorconfig`, configuração de VS Code e criados testes/reparo. A correção de arquivos em uma cópia isolada **não comprova aplicação de reparos em todos os assets do projeto principal**.

## 11.3. Histórico de testes relatados

| Marco | Resultado relatado |
|---|---:|
| Correção anterior de paredes | 136/136 Edit Mode |
| M2.9b.1 | 196/196 Edit Mode |
| M2.10a (pisos) | 205/205 Edit Mode; piso também validado manualmente |
| M3a | 210/210 Edit Mode |
| M3b | 215/215 Edit Mode |
| Correção UTF-8 | 217/217 Edit Mode em cópia isolada |
| Empilhamento M3b.1 | **227/227 Edit Mode** em cópia temporária isolada, **antes da correção do erro geométrico A/B** |

Regra: **compilação ≠ teste Edit Mode ≠ teste Play Mode ≠ validação visual**. Resultados automatizados podem ter especificação errada, como ocorreu com a rotação global do layout B.

## 11.4. Legados e dívidas técnicas identificadas (não afirmar remoção)

- Nomes antigos `Prototype` em assets/ferramentas; migrações para nomes definitivos iniciadas/documentadas.
- Prefab `PrototypeShelf` antigo aparentemente sem referências textuais/GUID observadas em auditoria; validar antes de excluir.
- Hierarquia antiga de 20 ProBuilder bins coexistia em auditoria com FBX de 25 gavetas; não remover sem validação específica.
- Materiais de argamassa `TEX_SacoArgamassa20kg.mat` (material, não textura) e `MAT_SacoArgamassa20kg.mat` possivelmente redundantes; validar referências e aparência antes de limpar.
- `FurniturePlacementController.cs` tinha cerca de 1.557 linhas na auditoria. Pode merecer decomposição **futuramente**, nunca no meio de correção pontual.
- Cenas/Build Settings tiveram referências antigas a `StorePrototype`/`SampleScene`; a cena atual reportada é `Store.unity`, mas não assumir que todas as referências antigas foram limpas.

Usar `AssetDatabase.MoveAsset` ou fluxo seguro preservando `.meta`/GUID ao movimentar assets Unity, verificando caminhos literais em ferramentas Editor.

---

# 12. Roadmap operacional vigente

> A numeração mudou entre GDD v0.1, v0.2 e v0.3. Abaixo está a organização operacional mais recente nas conversas, não uma promessa de que todos os módulos estão terminados.

| Marco | Objetivo | Situação |
|---|---|---|
| **M0** | Fundação, câmera, grid, cena e posicionamento inicial | Base implementada. |
| **M1** | Móveis e inventário/slots iniciais | Base funcional implementada. |
| **M2** | Paredes, diagonais, portas e pisos | Principais mecânicas implementadas; acabamento de junções pendente. |
| **M3a** | Ampliação de produtos | Quatro novos SKUs implementados. |
| **M3b** | Palete e gôndola dupla | Prefabs e lógica iniciais; integração dos modelos definitivos em andamento. |
| **M3b.1** | Layout de 40 sacos/8×5 | Implementação inicial testada; correção A/B **não confirmada**. |
| **M4** | Fornecedores, compras, pedidos, entregas e recebimento | Não iniciado como sistema completo. |
| **M5** | Clientes reais, caixa e vendas | Não iniciado como sistema completo; há apenas vendas simuladas. |
| **M6** | Funcionários, automação, finanças e expansão | Não iniciado. |
| **M7** | Save/load, balanceamento e polimento | Não iniciado como etapa completa. |

### Regra estratégica de produto

Antes de ampliar indiscriminadamente o catálogo de móveis e modelos, fechar o ciclo de uma loja: **comprar → receber → armazenar → abastecer → vender → registrar resultado → reinvestir**. A Visão Cidade deve enriquecer a demanda econômica, e não ser construída como sistema paralelo sem integração.

---

# 13. Prioridades de continuação

## P0 — Concluir a M3 atual, sem recomeçar sistemas

1. **Verificar no projeto principal** se a correção do `PalletBagDisplayLayout` (inverter faixas A/B, nunca girar camada inteira) já foi aplicada. Se não, aplicar correção mínima usando Codex quando solicitado; atualizar testes relevantes.
2. **Confirmar** se o menu `Configure Pallet Bag Display` foi executado e os campos no `DEF_PalletBagDisplay` estão em capacidade 40, surface 1,20×1,00 e height 0,15, sem alterar o prefab visual manual.
3. **Verificar se existe** `PF_Cimento50kg`; caso ausente, criar prefab visual a partir do FBX individual já importado, sem colliders desnecessários e sem alterar seu modelo.
4. **Confirmar campo real** de referência visual no `ProductDefinition` de `cimento_50kg` e vincular o prefab. Não presumir o nome exato de campo ou arquivo sem inspeção.
5. Validar em **Play Mode do projeto principal** quantidades 0, 1, 3, 5, 7, 20, 39 e 40, alternância, altura, rotação, mover/remover expositor, retirada de unidades e integridade do estoque.
6. Observar interferência do modelo 1,20 m com grid 1×1; documentar resultado sem modificar dimensões por conta própria.

Critério de aceite: **a quantidade visual coincide com a lógica, nenhuma camada invade a superfície, o palete mantém layout ao mover/rotacionar, não surgem placeholders e as referências persistem após salvar a cena/prefab.**

## P1 — Estabilizar demais móveis da M3

- Confirmar slots de ferramentas e 25 bins independentes.
- Confirmar apresentação de produtos no conjunto de móveis já existentes.
- Integrar modelos definitivos da gôndola, tinta, trena e buchas quando o desenvolvedor os finalizar; não bloquear o progresso do ciclo comercial por causa de arte não crítica.
- Revisar HUD da operação/construção/administração depois de esclarecer fluxo comercial.

## P2 — Primeiro ciclo comercial integral

- Compra simplificada com saldo financeiro em centavos.
- Fornecedor, pedido e recebimento no depósito.
- Reposição manual da exposição.
- Venda transacional única reduzindo slot correto e atualizando caixa/receita, sem duplicação.
- Regras mínimas de preço, margem e demanda para gerar decisões interessantes.
- Teste jogável: construir pequena loja → abastecer → vender → reinvestir.

## Mais adiante

- Clientes visuais, atendimento e caixa, funcionários, automação, cidade economicamente ativa, filiais e persistência/saves, de acordo com dependências e roadmap aprovado.
- Refino da geometria das junções de paredes, limpeza de legados e otimizações após necessidade demonstrada.

---

# 14. Parâmetros antigos que são apenas propostas

O GDD v0.1 registrou parâmetros experimentais como **capital inicial R$ 50.000**, **dia com seis minutos reais** e velocidades **1×/2×/4×**. Também propunha uma cidade inicial com três bairros e um catálogo de dez produtos. Esses valores **não estão confirmados como balanceamento vigente nem como sistemas implementados**.

Outras questões abertas de game design: regras de compra/aluguel de imóvel, evolução da cidade, horário comercial, empregados e automação, custos de construção, venda por unidade/pacote/peso/m³, empréstimos, contratos de obras, tráfego, competição, múltiplos andares e economia de filiais. Não preencher essas lacunas com decisões automáticas do assistente.

---

# 15. Critérios de qualidade para novas tarefas de Codex

Antes de escrever código:

1. Declarar o **objetivo único** e os critérios de aceite; identificar dependências existentes.
2. Ler apenas os arquivos relevantes e seguir `AGENTS.md`/documentação aplicável. Não executar auditoria completa sem motivo.
3. Respeitar IDs, GUIDs, prefab roots, componentes, `ScriptableObjects`, cenas e materiais.
4. Manter estado mutável de estoque/caixa fora dos assets estáticos de definição.
5. Fazer alterações mínimas; se houver incompatibilidade, primeiro documentar causa e efeito.
6. Não criar objetos visuais artísticos ou placeholders, nem mudar footprint/capacidade sem decisão aprovada.
7. Adicionar/ajustar testes direcionados quando apropriado.
8. Relatar arquivos alterados, resultados executados de fato, ambiente de execução, limitações e roteiro de validação Play Mode.
9. Não anunciar sucesso visual a partir de compilação/qualquer suíte de Edit Mode.

**Assistente principal:** só elaborar prompt para Codex quando solicitado; caso contrário, discutir a arquitetura, a experiência e o raciocínio antes da implementação.

---

# 16. Controle de continuidade e atualização deste documento

Este arquivo deve ser mantido junto ao repositório preferencialmente em:

`MaterialCityTycoon\Documentation\PROJECT_MASTER.md`

Ao concluir um marco, alterar **somente** as seções afetadas e adicionar uma entrada curta ao histórico abaixo. Evitar reescrever decisões sem registro da mudança. Uma afirmação “validado” precisa indicar **onde/como** foi validado.

## Registro de atualizações

| Data | Versão | Alteração | Evidência |
|---|---|---|---|
| 05/11/2026 | 1.0 | Consolidação inicial do GDD v0.1/v0.2/v0.3, catálogo de marcas v0.4, histórico da Unity e integração do cimento | Documentos recuperados, relatórios do Codex e confirmações do desenvolvedor em conversa |

## Estado que deve ser informado no começo de uma nova conversa

- Estamos na **M3**, integrando os modelos definitivos ao sistema de exposição existente.
- O palete definitivo está integrado e o footprint 1×1 foi validado no preview.
- O FBX de cimento 50 kg foi importado corretamente em 05/11.
- **Ainda não há confirmação** do prefab visual final do cimento nem do vínculo à definição do produto.
- **Ainda não há confirmação** de aplicação da correção A/B no algoritmo; o último resultado foi 227 testes aprovados **antes** da correção.
- Não reimplementar estoque/posicionamento nem alterar mecanicamente o FBX.
- A próxima etapa é confirmar o estado real dessas integrações e fazer o teste Play Mode do empilhamento 0–40.

---

# 17. Fontes e precedência

Referências históricas utilizadas na consolidação:

1. `Material_City_Tycoon_GDD_v0.1.md` — documento inicial de conceito.
2. `Material_City_Tycoon_Consolidacao_GDD_v0.2.md` — classificação entre decisões, propostas e questões abertas, datado de 22/10/2026.
3. Escopo técnico/GDD v0.3 de 22/10/2026 — visão, arquitetura, roadmap e fluxo Codex.
4. `Material_City_Tycoon_Catalogo_Marcas_v0.4.md` — marcas provisórias e triagem de 23/10/2026.
5. Relatórios e documentos de transição apresentados em conversas posteriores (incluindo relatos de M2, M3a, M3b e M3b.1).
6. Confirmações diretas do desenvolvedor sobre modelagem, importação, preview, configuração e testes manuais.

**Precedência:** decisões atuais confirmadas pelo desenvolvedor > relatórios recentes de implementação/teste > especificações anteriores de planejamento. Quando houver conflito, destacar a divergência em vez de ocultá-la.

**Limitação:** não foi feita leitura direta do repositório Unity no computador do desenvolvedor nesta consolidação; caminhos e estados de runtime devem ser verificados ao executar a próxima tarefa.
