# Local recovery of the already pinned upstream source. Not a production image qualification.
FROM golang:1.24-bookworm AS build
ENV CGO_ENABLED=0 GOTOOLCHAIN=local GOBIN=/out
RUN go install github.com/minio/minio@RELEASE.2025-04-08T15-41-24Z \
    && cp /go/pkg/mod/github.com/minio/minio@*/LICENSE /out/LICENSE

FROM curlimages/curl:8.12.1
USER root
RUN mkdir -p /data /home/minio && chown -R 10001:10001 /data /home/minio
COPY --from=build /out/minio /usr/local/bin/minio
COPY --from=build /out/LICENSE /usr/share/licenses/minio/LICENSE
ENV HOME=/home/minio
LABEL org.opencontainers.image.source="https://github.com/minio/minio" \
      org.opencontainers.image.version="source-RELEASE.2025-04-08T15-41-24Z"
USER 10001:10001
EXPOSE 9000 9001
ENTRYPOINT ["/usr/local/bin/minio"]
CMD ["server", "/data", "--console-address", ":9001"]
