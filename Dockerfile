FROM maven:3.9.9-eclipse-temurin-21 AS build
WORKDIR /workspace

COPY .mvn/ .mvn/
COPY mvnw pom.xml ./
RUN ./mvnw -B -q -DskipTests -Dmaven.wagon.http.retryHandler.count=5 dependency:go-offline

COPY src ./src
RUN ./mvnw -B -q -DskipTests -Dmaven.wagon.http.retryHandler.count=5 clean package

FROM eclipse-temurin:21-jre
WORKDIR /app

COPY --from=build /workspace/target/*.jar app.jar

EXPOSE 8080
ENTRYPOINT ["java", "-jar", "/app/app.jar"]
