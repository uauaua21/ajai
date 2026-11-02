# Contributing

1. Read the README and architecture notes.
2. Create a focused branch for your change.
3. Keep secrets and runtime data outside Git.
4. Add integration coverage when changing permissions or API behavior.
5. Run `dotnet build AJAI.sln --configuration Release` and `dotnet test AJAI.sln --configuration Release --no-build`.
6. For AI changes, install `ai/requirements.lock.txt`, run `python ai/download_model.py`, then `python -m unittest discover -s ai/tests -v`. Keep model weights and private images out of Git.
7. Describe the problem, behavior change, and verification in your pull request.

Use English for new public documentation and comments. Keep API field names and SignalR event names stable unless a change is documented. Do not present external AI or UI components as included without their actual source and setup instructions.

No contributor license agreement or open-source license is currently established. Agree on ownership and licensing with the maintainers before contributing substantial work.
