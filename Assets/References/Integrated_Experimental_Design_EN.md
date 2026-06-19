# ADBrush Comprehensive Experimental Design

## 1. Overview

This document outlines the comprehensive experimental design for the ADBrush system. This research aims to address two core problems in existing VR spatial drawing tools:

1.  **Limited Rolling Direction Control:** Existing ribbon-based brush methods (such as AdaptiBrush) struggle to achieve continuous and precise twist/rolling control during the drawing process.
2.  **Fixed Cross-Sections:** Existing tools are typically limited to fixed line-segment cross-sections, lacking custom shapes and real-time deformation capabilities, which restricts expressiveness.

ADBrush addresses the rolling control problem by proposing a new **"click-drag-rotate" interaction paradigm**, and solves the shape limitation through **Base Shape Editor and Morph Editor**.

This experimental design aims to validate the effectiveness of the above solutions:
- **Experiment 1** compares with AdaptiBrush to verify whether the new interaction paradigm effectively improves **rolling direction control** precision.
- **Experiment 2** compares fixed vs. custom brushes to verify whether custom cross-section functionality significantly enhances **creative freedom and expressiveness**.
- **Experiment 3** compares with Blender to verify the advantages of the VR-based generalized cylinder modeling paradigm over traditional desktop tools.

This design follows the structure of Chapter 6 (Evaluation) in the thesis draft and references methodologies from recent HCI research, including counterbalanced within-subject designs and objective performance metrics.

---

## 2. Research Questions (RQs)

This evaluation aims to answer the following research questions, which correspond to the challenges proposed in the thesis:

- **RQ1 (Rolling Control and Efficiency):** Compared to existing VR drawing tools (AdaptiBrush), can ADBrush's "click-drag-rotate" paradigm significantly improve the control precision and drawing efficiency of curve **rolling direction**?
- **RQ2 (Expressiveness and Flexibility):** Compared to tools that only provide fixed cross-sections, does introducing **custom cross-sections and real-time morphing** functionality significantly enhance users' creative freedom and satisfaction?
- **RQ3 (Modeling Paradigm Comparison):** When completing specific geometric modeling tasks, what are the differences between ADBrush's immersive workflow and traditional desktop modeling tools (Blender) in terms of task time, learning cost, and ease of use?

---

## 3. Methodology

### 3.1 Participants

This study involves multiple experiments with different participant groups:

#### Participants for Experiments 1 & 2
- **Target Sample Size:** 16 valid participants.
- **Recruitment Target:** University students and researchers.
- **Inclusion Criteria:**
  - Normal or corrected-to-normal vision.
  - No history of severe motion sickness.
  - VR or 3D modeling experience, if any, should be recorded for stratified analysis.

#### Participants for Experiment 3a (Blender User Comparison)
- **Target Sample Size:** At least 3 Blender users.
- **Source:** May be selected from Experiment 1 & 2 participants (as they have been exposed to the VR environment), but Blender experience must be additionally confirmed. If not from Experiment 1 & 2 participants, ADBrush training is required before the experiment.
- **Inclusion Criteria:** Has Blender experience, able to use Blender's curve sweep, Bevel/Taper, or Geometry Nodes functions to complete generalized cylinder modeling.

#### Participants for Experiment 3b (Third-Party Evaluators)
- **Target Sample Size:** 10 evaluators.
- **Source:** Must be **completely different** from Experiment 1 & 2 participants, and have **no prior exposure** to ADBrush or Blender.
- **Inclusion Criteria:** No VR modeling or Blender experience to ensure objectivity of evaluation.

### 3.2 Apparatus

- **Hardware:**
  - VR Headset: Meta Quest 2 (connected via Quest Link or SteamVR streaming).
  - PC: Windows 10/11, equipped with dedicated GPU.
  - Input Device: Touch Controllers (6DoF).
  - Play Area: Seated or standing, with 2m × 2m cleared space around.
- **Test Software/Interfaces:**
  - **ADBrush:** The proposed system under evaluation (including Base Shape Editor and Morph Editor).
  - **AdaptiBrush:** Replicated version (ribbon brush baseline).
  - **Blender:** Standard desktop version (for Experiment 3).

### 3.3 General Procedure

Within-subjects design is adopted to minimize individual differences. Tool/condition order will be **counterbalanced** (e.g., Latin Square design) to eliminate order effects.

**Complete Procedure:**

0.  **Recruitment:** Recruit for Experiments 1, 2, and 3a. If unable to recruit required personnel for Experiment 3a, recruit according to 3b.
1.  **Introduction (5 min):** Brief overview of research objectives, safety guidelines, and signing of informed consent.
2.  **Pre-Study Questionnaire (5 min):** Collect demographic information, VR experience, and 3D modeling expertise.
3.  **Training 1 → Experiment Task 1 → Questionnaire 1**
4.  **Training 2 → Experiment Task 2 → Questionnaire 2**
5.  **Training 3 (required for 3a, not needed for 3b) → Experiment Task 3 → Questionnaire 3**
6.  **Post-Study Interview (10 min):** Semi-structured interview to collect qualitative feedback.

*Note: Each training phase is approximately 10-15 minutes.*

---

## 4. Experiment 1: Comparison with AdaptiBrush (Rolling Control Validation)

**Objective:** Address **RQ1**. Validate the effectiveness of the "click-drag-rotate" paradigm in solving the rolling direction control problem.

### 4.1 Design

- **Design:** Within-subjects design (repeated measures).
- **Independent Variable:** System type (ADBrush vs. AdaptiBrush).
- **Note:** AdaptiBrush itself is a ribbon brush, serving as the existing technology baseline. ADBrush uses an equivalent default line-segment cross-section brush but enables the "click-drag-rotate" interaction paradigm.

### 4.2 Tasks

Participants will trace 3D reference shapes displayed in the virtual environment. Reference shapes are presented as **semi-transparent guide meshes**, and users should replicate their position and normal direction as accurately as possible. The system will automatically record time, position error, and angular error through the program.

**Test Set Design (8 shapes total, divided into 2 groups of 4 each):**

- **Group 1 (Spatial curve trajectory, no twist):**
  1.  Planar uniform outward spiral trajectory, but with normals perpendicular to both the plane and tangent.
  2.  Cylindrical helix trajectory, but with normals perpendicular to tangent but within the plane.
  3.  Trajectory composed of multiple line segments and right-angle turns. Normal directions are random but consistent within each segment, ensuring shape continuity at right-angle turn sections.
  4.  Trajectory following a circular path along the base of a cone, but with normals pointing toward the apex of the cone.

- **Group 2 (Spatial curve trajectory, with twist):**
  1.  Circular trajectory, but the ribbon forms a Möbius strip (180° rotation when connecting to origin).
  2.  Circular trajectory, left-rotate 180° then right-rotate 180° (total rotation is 0 when connecting to origin).
  3.  Straight-line trajectory, but normal direction continuously rotates along the trajectory (like a DNA single strand, continuously twisting while ascending along Z-axis).
  4.  Spatial figure-8 trajectory, left-rotate 90° then right-rotate 90° then left-rotate 90° then right-rotate 90° (total rotation is 0).

### 4.3 Metrics

#### Objective Metrics (Automatically Recorded by Program)

| Metric Name | Definition | Unit |
|-------------|------------|------|
| **Task Completion Time** | Time from first control point placement to user submission | Seconds (s) |
| **Position Error** | Uniformly sample points on user's curve, calculate average of minimum distances from each point to reference curve centerline | Millimeters (mm) |
| **Angular Error (Key Metric)** | Uniformly sample points on user's curve, calculate average angular deviation between user-drawn normal direction and corresponding reference curve normal direction at each point | Degrees (°) |

#### Subjective Metrics (Post-Experiment Questionnaire, 0-10 Scale)

| Question ID | Question Content | Rating Range |
|-------------|------------------|--------------|
| E1-Q1 | Do you find this tool **easy to control**? | 0-10 |
| E1-Q2 | What is your **overall satisfaction** with using this tool to complete the tracing task? | 0-10 |

---

## 5. Experiment 2: Expressiveness Validation (Fixed vs. Custom Brushes)

**Objective:** Address **RQ2**. Validate whether custom cross-sections and morphing functionality solve the "fixed cross-section shape" problem and enhance creative freedom.

### 5.1 Design

- **Design:** Within-subjects design.
- **Independent Variable:** Brush mode.
  - **Condition A (Fixed Ribbon):** Only use default line-segment cross-section, no morphing functionality.
  - **Condition B (Custom Cross-Section + Morphing):** Allow use of Base Shape Editor and Morph Editor to create custom brushes.

### 5.2 Tasks

**Prompted Creation Task.** We designed 8 creation themes. Each participant will randomly draw 2 different themes and create using different conditions for each. Each participant completes a total of two creations. AB order and BA order are randomized.

**8 Creation Themes:**
1.  Seaweed
2.  Peach Blossom Branch
3.  Desk Lamp
4.  Gift Box
5.  Beer Bottle
6.  Suitcase
7.  Cannon
8.  Croissant

To facilitate drawing, participants are allowed to reduce the size of the original objects.
**Time Limit:** Each creation session is 5-15 minutes (participants decide completion time, but not exceeding 15 minutes).

### 5.3 Metrics

- **Subjective Metrics (0-10 Scale):**
  - Overall rating for Condition A and Condition B tools (user preference).
  - Creative freedom under Condition A and Condition B (How many shapes can I create with this condition? Would the range of subjects differ?).
  - Artwork satisfaction rating under both conditions (Would Condition B result in greater satisfaction than Condition A?).
  - Tool preference: "If doing free creation, do you think B and A have significant differences in creative freedom?"
- **Open Questions:**
  - Interview question: "How do you think the ability to customize brush shapes influenced your design process?"

---

## 6. Experiment 3: VR vs. Desktop Modeling (Blender) Comparison

**Objective:** Address **RQ3**. Compare the proposed VR workflow with traditional desktop tools on generalized cylinder modeling tasks.

**Important Note:** Due to Blender's steep learning curve, ordinary users find it difficult to master its curve sweep or loft functions in a short time. Therefore, this experiment designed two alternative approaches, to be executed based on recruitment situation:

### Approach 3a: Expert Direct Operation Comparison (Priority Approach)

**Applicable Condition:** Able to recruit at least **3** users proficient in Blender for curve/surface modeling.

#### 6a.1 Design
- **Design:** Within-subjects design.
- **Independent Variable:** Modeling tool (ADBrush vs. Blender).
- **Participants:** Blender experts (able to use Blender's Curve, Bevel/Taper, Geometry Nodes, or Loft plugins).

#### 6a.2 Tasks
Participants use both tools to create specified 3D swept surfaces:
1.  **Task 3a.1 (Simple Sweep):** Sweep a circular cross-section along a spatial curve to generate a tubular structure.
2.  **Task 3a.2 (Variable Cross-Section Sweep):** During a 90-degree bend, cross-section transitions from circular to square (Loft/Morph effect).

#### 6a.3 Metrics
- **Objective Metrics:**
  - Task completion time (s).
- **Subjective Metrics (0-10 Scale):**
  - Tool efficiency rating.
  - Tool intuitiveness rating.
  - Tool preference: "Which tool would you choose for rapid prototyping?"
- **Open Question Feedback:**
  - Evaluation of pros and cons of both tools in surface modeling.
  - Explanation of preference reasons.

---

### Approach 3b: Third-Party Evaluator Observation Comparison (Alternative Approach)

**Applicable Condition:** Unable to recruit sufficient Blender experts (fewer than 3), then adopt this approach.

#### 6b.1 Design
- **Design:** Third-party evaluation study.
- **Participants:** Recruit **10 evaluators** (no Blender or VR experience required), watch pre-recorded videos.

#### 6b.2 Materials
Researchers collect or record the following videos:
1.  **Video Group A (Blender):** Collect tutorial or demonstration videos from YouTube and other platforms showing Blender's generalized cylinder modeling workflow (such as curve sweep, Bevel operations, etc.).
2.  **Video Group B (ADBrush):** Recorded during Experiments 1 and 2, showing the operation process of using ADBrush to complete similar modeling concepts (VR screen recording + narrated instructions).

*Note: Due to fundamental differences in operation paradigms and output formats between the two tools, videos are not required to show exactly identical modeling targets, but rather demonstrate each tool's typical workflow for completing the concept of "generating swept surfaces along curves." Evaluators will compare based on perceived learnability, efficiency, and intuitiveness.*

#### 6b.3 Tasks
Evaluators watch two videos (order counterbalanced), then complete the evaluation questionnaire.

#### 6b.4 Metrics
- **Subjective Metrics (0-10 Scale):**
  - Perceived learning difficulty: "Does this tool look easy to learn?"
  - Perceived efficiency: "Does this tool look efficient at completing tasks?"
  - Perceived intuitiveness: "Does the operation process look intuitive?"
  - Tool preference: "If you were to learn 3D modeling, which tool would you choose to start with?"
- **Open Questions:**
  - "What are the obvious differences between the two tools' operation processes?"
  - "Which tool do you think is more suitable for beginners? Why?"

---

## 7. Data Analysis Plan

### 7.1 Quantitative Analysis

- **Descriptive Statistics:** Mean and Standard Deviation (SD) of time, error, and ratings.
- **Hypothesis Testing:**
  - **Paired t-test:** For comparing ADBrush vs. AdaptiBrush and ADBrush vs. Blender.
  - **Effect Size:** Use Cohen's *d* to measure the magnitude of differences.
  - **Significance Level:** α = 0.05.

### 7.2 Qualitative Analysis

- **Thematic Analysis:** Transcribe interview content and identify recurring themes.
- **Observation Records:** Record key events or difficulties during tasks.

---

## 8. Appendix: Questionnaire Content

### 8.1 Pre-Study Questionnaire (All Participants)

| Question ID | Question Content | Response Type |
|-------------|------------------|---------------|
| P1 | What is your age? | Number |
| P2 | What is your gender? | Single choice: Male / Female / Other / Prefer not to say |
| P3 | What is your major/occupation? | Text |
| P4 | Do you have experience using VR devices? | Single choice: Never used / Yes |
| P5 | Do you have experience with 3D modeling software? | Single choice: No / Yes |
| P6 | If you have 3D modeling experience, please list the software you are familiar with: | Text (optional) |

---

### 8.2 Experiment 1 Questionnaire: Comparison with AdaptiBrush (One copy per tool, two copies per person)

**Tool Name:** ☐ ADBrush / ☐ AdaptiBrush

| Question ID | Question Content | Rating Range |
|-------------|------------------|--------------|
| E1-Q1 | Do you find this tool **easy to control**? | 0-10 |
| E1-Q2 | What is your **overall satisfaction** with using this tool to complete the tracing task? | 0-10 |

**Open Questions:**
- E1-O1: What difficulties or inconveniences did you encounter when using this tool?

---

### 8.3 Experiment 2 Questionnaire: Expressiveness Validation (Q section: one copy per condition, two copies per person)

**Condition Name:** ☐ Condition A (Fixed Ribbon) / ☐ Condition B (Custom Cross-Section + Morphing)

| Question ID | Question Content | Rating Range |
|-------------|------------------|--------------|
| E2-Q1 | What is your **overall rating** for the tool under this condition? | 0-10 |
| E2-Q2 | What is your perceived **creative freedom** using this condition? (Range of subjects you can create) | 0-10 |
| E2-Q3 | What is your **artwork satisfaction** for work completed under this condition? | 0-10 |

**Comparison Questions After Completing Both Conditions:**

| Question ID | Question Content | Response Type |
|-------------|------------------|---------------|
| E2-C1 | If doing free creation, which condition would you choose? | Single choice: Condition A / Condition B / No clear preference |
| E2-C2 | Do you think Condition B improves creative freedom compared to Condition A? | Single choice: Yes / No |

**Open Questions:**
- E2-O1: How do you think the ability to customize brush shapes influenced your design process?

---

### 8.4 Experiment 3a Questionnaire: VR vs Blender Expert Comparison (Q section: one copy per tool, two copies per person)

**Tool Name:** ☐ ADBrush / ☐ Blender

| Question ID | Question Content | Rating Range |
|-------------|------------------|--------------|
| E3a-Q1 | What is the **efficiency** of using this tool to complete such swept surface tasks? | 0-10 |
| E3a-Q2 | What is the **intuitiveness** of this tool's operation process? | 0-10 |
| E3a-Q3 | What is your **overall satisfaction** with using this tool to complete the task? | 0-10 |

**Comparison Questions After Completing Both Tools:**

| Question ID | Question Content | Response Type |
|-------------|------------------|---------------|
| E3a-C1 | Which tool would you choose for rapid prototyping? | Single choice: ADBrush / Blender / No clear preference |
| E3a-C2 | Which tool would you choose for detailed modeling? | Single choice: ADBrush / Blender / No clear preference |

**Open Questions:**
- E3a-O1: Please briefly describe the pros and cons of both tools in surface modeling.

---

### 8.5 Experiment 3b Questionnaire: Third-Party Evaluator Observation Comparison (Complete after watching both videos. Viewing order randomized. Q section: one copy per condition, two copies per person.)

**Video Name:** ☐ Video Group A (Blender) / ☐ Video Group B (ADBrush)

| Question ID | Question Content | Rating Range |
|-------------|------------------|--------------|
| E3b-Q1 | Does this tool look **easy to learn**? | 0-10 |
| E3b-Q2 | Does this tool look **efficient** at completing tasks? | 0-10 |
| E3b-Q3 | Does this tool's operation process look **intuitive**? | 0-10 |

**Comparison Questions After Watching Both Videos:**

| Question ID | Question Content | Response Type |
|-------------|------------------|---------------|
| E3b-C1 | If you were to learn 3D modeling, which tool would you choose to start with? | Single choice: Blender / ADBrush / No clear preference |

**Open Questions:**
- E3b-O1: What are the obvious differences between the two tools' operation processes?
- E3b-O2: Do you think ADBrush is easier for beginners to get started with? Why?

---

### 8.6 Post-Study Interview Guide

1.  What impressed you most during the entire experiment?
2.  What do you think needs the most improvement in ADBrush?
3.  Do you think the "click-drag-rotate" interaction method is easier to control than continuous drawing? What suggestions do you have for the interaction method?
4.  (Experiment 2 only) Did the custom cross-section and morphing functionality help you create more shapes? What suggestions do you have for the interaction method?
5.  What expectations or suggestions do you have for the future development of VR spatial drawing tools?
