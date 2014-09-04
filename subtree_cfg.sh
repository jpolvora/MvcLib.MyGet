# Adiciona a subtree p/ pull e atualiza
git remote add --fetch MvcLib https://jpolvora@bitbucket.org/jpolvora/mvclib.git
git subtree add --prefix=MvcLib MvcLib master --squash