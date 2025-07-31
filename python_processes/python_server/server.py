from flask import Flask, request, jsonify
from model_def import AgentModel
import torch
import torch.nn as nn
import numpy as np
import json

device = torch.device("cuda" if torch.cuda.is_available() else "cpu")

app = Flask(__name__)


agent_models = {}
agent_models_history = {} # String/String dict that will communicate agent history to UNITY, not here.  
agent_general_scores = {}
agent_fitness_list = {}
pairwise_history = {}

DEFAULT_INPUT = 0.5 # When agent history isn't completely filled up by actual moves (0/1), we use this as an autofill. 
NUM_ROUNDS_PER_PAIR = 4
GENERATION_COUNT = 0 # This will (of course) go up

def calculate_score_matrix(action_i, action_j):

    if action_i == 0 and action_j == 0: # Split/Split 
        return 3, 3
    elif action_i == 0 and action_j == 1: # Split/Steal
        return 0, 5
    elif action_i == 1 and action_j == 0: # Steal/Split
        return 5, 0
    elif action_i == 1 and action_j == 1: # Steal/Steal
        return 1, 1
    
def mutate_weights(weights, mutation_strength=0.01):
    # Weights will be a a torch.Tensor
    noise = torch.randn_like(weights) * mutation_strength
    return weights + noise

@app.route('/models_init', methods=['POST'])
def initialize_agents():
    # Ok, the log checks here might be a little overkill from an outside view, but promise me, debugging django/unity communication errors is annoying
    data = request.get_json()
    if not data:
        return jsonify({'error': 'No JSON data received'}), 400
    num_agents = data.get('num_agents')
    if num_agents is None:
        return jsonify({'error': 'num_agents not provided'}), 400
    try:
        num_agents = int(num_agents)
    except (ValueError, TypeError):
        return jsonify({'error': 'num_agents must be an integer'}), 400
    
    for i in range(num_agents):
        model = AgentModel().to(device)
        model.eval()
        agent_models[f"agent_{i}"] = model
    return jsonify({'message': f'Initialized {num_agents} agents'}), 200

@app.route('/forward', methods=['POST'])
def forward_pass(): # Technically not really needed, but if you want to run individual simulations you can use this method to do so
    data = request.get_json()
    agent_id = data.get('agent_id')
    inputs = data.get('inputs') # Our list of 6 floats provided by the agent (3 for what moves agent played against opponent, 3 vice versa)
    
    if agent_id not in agent_models: # No agent model saved in our array
        return jsonify({'error': 'Unknown agent'}), 400
    if not inputs or len(inputs) != 6: # This shouldn't really happen, but just in case yk
        return jsonify({'error': 'Invalid inputs'}), 400

    model = agent_models[agent_id]
    tensor_input = torch.tensor([inputs], dtype=torch.float32)

    with torch.no_grad():
        output = model(tensor_input)
        probs = torch.softmax(output, dim=1).numpy()[0]
        action = int(np.argmax(probs)) # Binary output (prisoner's dilemma has only two options) where 0 will be to stay, 1 will be steal. 

    return jsonify({'action': action, 'probabilities': probs.tolist()})

@app.route('/play_dilemma', methods=['GET'])
def play_generation_dilemma():
    # Clear dicts/data per generation
    agent_general_scores = {}
    agent_fitness_list = {}
    pairwise_history.clear()

    # Read off our watts-strogatz connection json file
    with open('./ws_graph_edges.json', 'r') as f:
        edges = json.load(f)

    # Create history for each agent pair
    for agent_i, agent_j in edges:
        pair_key = tuple(sorted((agent_i, agent_j)))
        pairwise_history[pair_key] = {'i': [], 'j': []}

    actions_taken = {agent_name: [] for agent_name in agent_models}
    agent_score_history = {agent_name: [] for agent_name in agent_models}
    for agent_i, agent_j in edges:
        model_i = agent_models[f"agent_{agent_i}"]
        model_j = agent_models[f"agent_{agent_j}"]
        pair_key = tuple(sorted((agent_i, agent_j)))
        history_i = pairwise_history[pair_key]['i']
        history_j = pairwise_history[pair_key]['j']

        for round_num in range(NUM_ROUNDS_PER_PAIR):
            # Get our input histories
            input_i = ([DEFAULT_INPUT] * (3 - len(history_i)) + history_i)[-3:] + \
                        ([DEFAULT_INPUT] * (3 - len(history_j)) + history_j)[-3:]
            input_j = ([DEFAULT_INPUT] * (3 - len(history_j)) + history_j)[-3:] + \
                      ([DEFAULT_INPUT] * (3 - len(history_i)) + history_i)[-3:]

            input_tensor_i = torch.tensor([input_i], dtype=torch.float32).to(device)
            input_tensor_j = torch.tensor([input_j], dtype=torch.float32).to(device)

            with torch.no_grad():
                probs_i = torch.softmax(model_i(input_tensor_i), dim=1).cpu().numpy()[0]
                probs_j = torch.softmax(model_j(input_tensor_j), dim=1).cpu().numpy()[0]
                action_i = int(np.argmax(probs_i))
                action_j = int(np.argmax(probs_j))
                # Remember, 0 is to split, 1 is to steal

            # Update histories for both of our agents playing
            history_i.append(action_i)
            history_j.append(action_j)

            # Record actions for this round
            actions_taken[f"agent_{agent_i}"].append(action_i)
            actions_taken[f"agent_{agent_j}"].append(action_j)

            # SCORE MATRIX
            # Steal, Steal: (1, 1)
            # Split, Steal: (5, 0) / (0, 5)
            # Split, Split: (3, 3)
            score_i, score_j = calculate_score_matrix(action_i, action_j)

            name_i = f"agent_{agent_i}"
            name_j = f"agent_{agent_j}"
            agent_score_history[name_i].append(score_i)
            agent_score_history[name_j].append(score_j)
            agent_general_scores[name_i] = agent_general_scores.get(name_i, 0) + score_i
            agent_general_scores[name_j] = agent_general_scores.get(name_j, 0) + score_j

    for agent_name, score_list in agent_score_history.items():
        post_first_game_scores = score_list[NUM_ROUNDS_PER_PAIR:] # We'll cut out the first four rounds the agent has played from their fitness calculations, as they hadn't built up a memory at that point. 
        if post_first_game_scores:
            agent_fitness_list[agent_name] = sum(post_first_game_scores) / len(post_first_game_scores)
        else:
            agent_fitness_list[agent_name] = 0  # In the weird case one of our agents doesn't have a network larger than 1 other agent, I guess we'll just void their score?
            
    return jsonify({
        'message': 'Generation played!',
        'actions': actions_taken,
        'fitness_scores': agent_fitness_list,
        'general_scores': agent_general_scores
    })

@app.route('/mutate_generation', methods=['POST'])
def mutate_pruned_generation(): # We expect a one-dimensional list of agent names here
    pruned_agent_list = request.get_json()
    if not pruned_agent_list or len(pruned_agent_list) < 4:
        return jsonify({'error': 'Need at least 4 agents to mutate'}), 400
    
     # For our simulation, we'll have two offspring that are lightly mutated, and two that are heavily mutated for each agent of a previous generation. 
     # Light mutation will have strength of 0.01 for model weights, and heavy mutation will have a strength of 0.1
    new_models = {}

    for idx, agent_name in enumerate(pruned_agent_list[:4]):
        if agent_name not in agent_models:
            return jsonify({'error': f'Agent {agent_name} not found'}), 400
        original_model = agent_models[agent_name]
        new_model = AgentModel().to(device)
        new_model.load_state_dict(original_model.state_dict())  # copy weights
        new_model.eval()

        mutation_strength = 0.01 if idx < 2 else 0.1

        # Mutate the model's weights
        mutated_state_dict = {}
        for key, param in new_model.state_dict().items():
            mutated_param = mutate_weights(param, mutation_strength)
            mutated_state_dict[key] = mutated_param

        new_model.load_state_dict(mutated_state_dict)
        new_models[agent_name] = new_model

    # Replace original agent models with the newly mutated ones now
    for agent_name, mutated_model in new_models.items():
        agent_models[agent_name] = mutated_model # Save in our dictionary for seamless integration into play_dilemma (both reference same list)

    return jsonify({'message': f'Mutated 4 agents: {pruned_agent_list[:4]}'}), 200
    

if __name__ == '__main__':
    app.run(debug=True, port=8080)