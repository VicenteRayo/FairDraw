# Development-mode image: runs the Vite dev server with hot reload, matching how this project is
# meant to be worked on locally. A production build (multi-stage: `npm run build` + nginx serving
# `dist/`) is straightforward to add later but out of scope for this MVP — see README "Roadmap".
FROM node:22-alpine

WORKDIR /app

COPY package.json package-lock.json* ./
RUN npm install

COPY . .

EXPOSE 5173

CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]
