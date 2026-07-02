---
name: new-feature
description: Create a new feature branch for development. Use this whenever you want to start working on a new test or feature — fetches the latest integration, prompts for a feature name, creates feature/{name} branch, and checks it out so you're ready to code.
compatibility: Requires git
---

# New Feature Branch Setup

Quickly set up a new feature branch for development.

## What it does

1. Fetches the latest `integration` branch
2. Prompts you for a feature name
3. Creates a new branch `feature/{name}` off `integration`
4. Checks out the branch so you can start coding
5. Shows next steps

## Feature name format

Feature names should be:
- Lowercase
- Separated by hyphens (not spaces)
- Descriptive but concise

**Examples:**
- `add-login-tests`
- `fix-auth-flow`
- `update-homepage-config`

## How to use

Just ask to create a new feature:

```
I want to start working on adding login tests
```

Or use the slash command:
```
/new-feature
```

The skill will fetch the latest code, prompt you for a name, and set up your branch.

## Next steps after the skill runs

Once your branch is created and checked out:

1. **Make your changes** — write your tests or code
2. **Commit your work** — `git commit -m "your message"`
3. **Push to GitHub** — `git push origin feature/your-feature-name`
4. **Create a PR** — Open a PR from `feature/your-feature-name` → `integration`
