FROM mono:5.18.0.225

RUN apt update \
    && apt install -y \
        python3-pip \
        git

RUN python3 -m pip install --upgrade setuptools pip && python3 -m pip install jupyter

WORKDIR /
RUN git clone https://github.com/fsprojects/IfSharp.git
RUN mkdir notebooks
VOLUME notebooks

RUN useradd -ms /bin/bash ifsharp-user
RUN chown -R ifsharp-user /notebooks && chown -R ifsharp-user /IfSharp
USER ifsharp-user

WORKDIR /IfSharp
RUN ./build.sh
RUN mono bin/ifsharp.exe --install

EXPOSE 8888

ENTRYPOINT ["jupyter", \
            "notebook", \
            "--no-browser", \
            "--ip='*'", \
            "--port=8888", \
            "--notebook-dir=/notebooks" \
            ]
