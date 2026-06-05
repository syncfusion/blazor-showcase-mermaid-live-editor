#!groovy

node('AspComponent') {
    try {
        deleteDir();
        stage('Import') {
            git url: 'https://gitea.syncfusion.com/essential-studio/ej2-groovy-scripts.git', branch: 'master', credentialsId: env.GiteaCredentialID;
            blazor = load 'src/blazor.groovy';
        }
        stage('Checkout') {
            checkout scm;
            blazor.getProjectDetails();
            blazor.githubCommitStatus('running');
        }

        if(blazor.checkCommitMessage()) {
            stage('Install') {
                blazor.showcaseInstall();
            }

            stage('Build') {
                blazor.runShell('gulp dynamic-import && npm run minify && gulp blazor-showcase-build --option ' + blazor.fetchTargetBranch());
            }
          
            stage('Publish') {
                if(blazor.isProtectedBranch()) {
                    blazor.runShell('gulp blazor-showcase-azure-publish');
                }
            }
        }
        blazor.githubCommitStatus('success');
        deleteDir();
    }
    catch(Exception e) {
        blazor.throwError(e);
        deleteDir();
    }
}
