from flask import Flask, request, jsonify
from model_def import AgentModel
import torch
import torch.nn as nn
import numpy as np

app = Flask(__name__)
model = AgentModel()
model.eval()

agent_models = {}

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
        model = AgentModel()
        model.eval()
        agent_models[f"agent_{i}"] = model
    return jsonify({'message': f'Initialized {num_agents} agents'}), 200

@app.route('/forward', methods=['POST'])
def forward_pass():
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

if __name__ == '__main__':
    app.run(debug=True, port=8080)