# EvoDilemma 🎭
**An Evolutionary AI Simulation of the Iterated Prisoner’s Dilemma Across Complex Networks**

EvoDilemma simulates how artificial agents evolve strategic behavior over generations in the classic Iterated Prisoner’s Dilemma (IPD). Using neuroevolution and small-world network topologies, this project explores the emergence of cooperation, deception, and memory-based adaptation in agent populations.


## Overview

EvoDilemma models **600+ neural network agents** playing iterative IPD matches across dynamically generated **Watts-Strogatz small-world networks**. Each agent has a simple feedforward network that maps memory (of its last three moves and its opponent’s) to future actions.

After each generation, agents evolve using a **custom-built genetic algorithm** featuring:
- **Top-25% fitness-based selection**
- **4:1 offspring generation ratio (to maintain a consistent agent count in each generation)**
- **Crossover breeding** via random weight masking
- **Stratified mutation rates** (0.01 / 0.1 strength variants)

Fitness calculations exclude initial rounds to allow for **neural memory bootstrapping**, and **entire match histories** are logged for each agent across generations.

---

## 🔧 Key Features

### Evolution!
- Fitness-based selection and pruning
- Neuroevolution using PyTorch (no backpropagation, agents progressively get better through neurevolution, where agent fitness is calculated, and the top 25% get to further reproduce)

### Strategy Simulation
- Iterated Prisoner’s Dilemma tournaments
- Custom input encoding of 6 memory slots (3 agent, 3 opponent)
- Behavioral phenotyping (Cooperator / Defector tendencies)

### Network Dynamics
- Agents placed on Watts-Strogatz networks (rewiring prob configurable)
- Multiple neighbor games per agent per generation
- Edge visualization reflects interaction dynamics

### Real-Time Visualization (Unity)
- Interactive match history tracking for each agent
- Dynamic network graph with edge highlighting
- Generation progression control with coroutine-based automation
- Phenotype overlays 

### Distributed System Architecture
- **Unity frontend** (C#) communicates with **Flask backend** (Python)
- API endpoints for match simulation, mutation, generation progression
- Agent history synced across client/server

---

## Tech Stack

| Component       | Tech                            |
|----------------|----------------------------------|
| Simulation     | Unity (C#), REST API             |
| Backend Logic   | Python, Flask, PyTorch           |
| AI Methods      | Mutation Algorithms, 6-Input neural net |

---

## 📸 Screenshots
<img width="1900" height="858" alt="Agents being labeled as splitters(blue) or stealers(red)" src="https://github.com/user-attachments/assets/47750873-5dcc-4e9a-bb2f-9e2ebf9f157e" />
How our agent network looks after a generation progression (Blue nodes are agents that have a tendency to split more often, with red agents being more likely to steal). 
<img width="1890" height="861" alt="Viewing agent connections & match history" src="https://github.com/user-attachments/assets/981659d6-76f5-4074-868c-9aab55bc9713" />
How our agent network looks after an agent has been selected, showing agent match history against other connected agents. 
<img width="1891" height="856" alt="How a generation of agents look after pruning(top 25% of agents with the highest fitness scores kept)" src="https://github.com/user-attachments/assets/de32f497-4556-4852-82c4-f20055ab717d" />
How a generation of agents look after pruning(top 25% of agents with the highest fitness scores kept)

---
# Instructions for Running the Backend

## Prerequisites

- Python 3.10 or higher installed on your system  
- `pip` package manager available  

## Setup

### Create and activate a virtual environment (recommended):
If you don't already have an established venv that holds all of your dependencies, you can initialize a new one:

```bash
python3 -m venv venv
source venv/bin/activate   # On Windows use: venv\Scripts\activate
```
## Install dependencies
I WAS planning to dockerize this project, but for some reason, dockerization here was a pain, so you (the dear reader), will have to make do with installing the dependencies yourself. 
```bash
pip install --upgrade pip
pip install -r python_processes/requirements.txt
```
## Run the backend server:
```bash
python python_processes/python_server/server.py
```
After this is done(the port should be 8080), when the Unity simulation is ran, will automatically communicate with the server when needed (as it's set to communicate with 8080), no further setup required!
