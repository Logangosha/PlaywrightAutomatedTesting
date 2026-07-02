# Branching Strategy

This project uses a three-branch strategy for code review and validation.

## Branch Structure

```
production (stable, always good)
     ↑
     | (merge when validated)
     |
integration (staging, for testing)
     ↑
     | (merge after code review)
     |
feature/* (new tests/features)
```

## Branches

### `production`
- **Purpose**: Stable, production-ready code
- **Who can merge to it**: Only from `integration` via PR
- **Rules**: 
  - Never commit directly
  - Only merge after validation in integration
  - Should always be in a working state

### `integration`
- **Purpose**: Staging branch for testing and validation
- **Who can merge to it**: Feature branches via PR
- **Rules**:
  - Merge feature branches here first
  - Test and validate all changes
  - Then merge to `production` when ready

### `feature/*`
- **Purpose**: Individual feature/test development
- **Branch from**: `integration`
- **Naming convention**:
  - `feature/add-homepage-tests`
  - `feature/fix-auth-flow`
  - `feature/update-config`
- **Rules**:
  - Create off `integration`
  - Merge back to `integration` via PR
  - Delete after merge

## Workflow

### Creating a New Test/Feature

```bash
# 1. Start from integration
git checkout integration
git pull origin integration

# 2. Create feature branch
git checkout -b feature/your-feature-name

# 3. Make your changes, commit
git add .
git commit -m "Add feature description"

# 4. Push feature branch
git push origin feature/your-feature-name

# 5. Open PR to integration (via GitHub)
```

### Merging to Production

```bash
# 1. When feature is validated in integration
# 2. Create PR from integration → production

# 3. Merge when approved
```

## Code Review Process

1. **Feature PR to Integration**
   - Create PR from `feature/*` → `integration`
   - Describe changes and tests
   - Review and approve
   - Merge to `integration`

2. **Integration to Production**
   - Create PR from `integration` → `production`
   - Verify all tests pass in integration
   - Approve when validated
   - Merge to `production`

## Tips

- Keep feature branches small and focused
- Commit frequently with clear messages
- Delete feature branches after merging
- Use PR descriptions to explain changes
