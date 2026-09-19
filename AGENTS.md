# Project working rules

- Make the smallest possible change that solves the requested problem.
- Work only on the requested feature, genre, or problem. Do not modify unrelated features or genres.
- Inspect relevant code and settings before editing.
- Preserve known-working behavior.
- Never weaken tests to make them pass.
- Never weaken evaluator or rubric requirements.
- Run the targeted test after each meaningful change. Report any check that cannot run and why.
- Run `npm test` before declaring success when working in a Node/npm project with that test command. This repository is Unity/C#: use the relevant Unity compilation, tests, and Android build checks instead; do not add npm just for validation.
- A passing unit test does not prove Roblox Studio works. Likewise, Unity compilation or a successful APK build does not prove physical Android notification delivery works. Require the relevant runtime/device check before claiming that behavior is verified.
- Do not commit, merge, reset, or push unless explicitly asked.

## Git Bash requests

- When the user asks for "bash", provide a ready-to-copy Bash code block for Git Bash that brings them to the latest relevant changes. Include the project directory command using Git Bash path syntax.
- Inspect the current branch, remotes, and working-tree status before choosing commands. Distinguish local uncommitted changes from changes available remotely; do not imply local-only changes can be pulled.
- Provide commands for the user to run, rather than executing them. Preserve local work and do not include destructive reset or clean commands unless explicitly requested.

## Keep resource use modest

- Keep changes and tool output focused; avoid unnecessary dependencies, duplicate artifacts, broad searches, or repeated full builds without a new reason.
- Reuse one local test-build output location instead of accumulating copies.
- Keep Unity caches, logs, and generated builds out of Git using the existing `.gitignore`.
- Do not upload project files or generated artifacts to cloud storage unless requested.
- Do not delete user files or caches solely to save space without authorization.
- Distinguish disk/cloud storage from account usage limits; these practices do not guarantee reduced account usage.
