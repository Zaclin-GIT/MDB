# MDB Framework Documentation

This directory contains the source files for the MDB Framework documentation website, which is automatically deployed to GitHub Pages at [https://zaclin-git.github.io/MDB](https://zaclin-git.github.io/MDB).

## Structure

- `index.md` - Homepage
- `getting-started.md` - Getting started guide
- `api.md` - API reference overview
- `_api/` - Individual API documentation pages
- `_layouts/` - Jekyll layouts
- `_config.yml` - Jekyll configuration
- `assets/` - Images, CSS, and other static assets

## Deployment

The documentation is automatically deployed to GitHub Pages using GitHub Actions:

- **Trigger**: Automatically on push to `main` branch when files in `docs/` are modified
- **Manual trigger**: Available via "Actions" tab → "Deploy Documentation to GitHub Pages" → "Run workflow"
- **Workflow file**: `.github/workflows/deploy-docs.yml`

### How it works

1. The workflow checks out the repository
2. Jekyll builds the site from the `docs/` directory
3. The built site is uploaded as a GitHub Pages artifact
4. The artifact is deployed to GitHub Pages

### Prerequisites

For the workflow to work, GitHub Pages must be enabled in the repository settings:

1. Go to Repository Settings → Pages
2. Under "Build and deployment":
   - Source: GitHub Actions
3. Save the settings

## Local Development

To preview the documentation locally:

```bash
# Install Jekyll (if not already installed)
gem install jekyll bundler

# Navigate to the docs directory
cd docs

# Serve the site locally
jekyll serve

# Open in browser
# http://localhost:4000/MDB/
```

## Making Changes

1. Edit markdown files in this directory
2. Commit and push changes to `main` branch
3. GitHub Actions will automatically rebuild and deploy the site
4. Changes will be live at https://zaclin-git.github.io/MDB within a few minutes

## Troubleshooting

- **Workflow fails**: Check the Actions tab for error logs
- **Site not updating**: Ensure GitHub Pages is configured to use "GitHub Actions" as the source
- **404 errors**: Verify the `baseurl: /MDB` setting in `_config.yml` matches your repository name
