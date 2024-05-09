### THIS SCRIPT IS TO BE EXECUTED AFTER DEVOPS RUN OF JGDokRouterLLMDemo-CI
### IT WILL TAKE AS INPUT THE BUILD NUMBER CREATED BY DEVOPS
### REASON IT IS HERE IS BECAUSE THE DevOps CANNOT BUILD JGDokRouterLLMDemo AS IT IS NOT SOURCE CONTROLLED
param 
(

      [parameter(Mandatory = $true)]
      [string] $buildNumber
)

#Setup variables
$releasePath = "//fs1.infosistema.com/joyndevops/Releases/jgdokrouterllmdemo/$buildNumber"

#Build Docker Image
docker build -f "C:\Projects\Joyn\JGDokRouter\docker\Joyn.DokRouterLLMDemo.Dockerfile" -t jgdokrouterllmdemo:v$buildNumber -t jgdokrouterllmdemo:latest "C:\Projects\Joyn\JGDokRouter\sources"

docker save jgdokrouterllmdemo:v$buildNumber --output "$releasePath/jgdokrouterllmdemo.tar" jgdokrouterllmdemo:v$buildNumber


cp "C:\Projects\Joyn\JGDokRouter\docker\Joyn.DokRouterLLMDemo.DockerCompose.yml" "//fs1.infosistema.com/joyndevops/Releases/jgdokrouterllmdemo/$buildNumber"