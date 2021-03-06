Основная идея
=============

Поскольку мир бесконечный, использование плоского представления данных будет недостатком.
Я использую квадратичное сбалансированное дерево. Вершинами дерева являются 
блоки карты (двумерная матрица с весами вероятности появления травы). 
В узлах хранятся общие веса детей. 
В начале игры дерево состоит только из одной вершины и из одного узла (корневого) и растет
в процессе генерации мира. Мир генерируется по-блочно. Каждый блок появляется по соседству с текущим, 
в момент приближения игрока к краю блока.

Для идентификации блоков я использую пути. Путь - это последовательность ориентаций (внутри узла), которая 
однозначно определяет конечную вершину. Можно представить путь в виде строки, например "0/2/3" - это знчит,
что в корневом узле я выбираю верхнюю левую ориентацию (0), в слудующем узле нижнюю левую (2) и затем 
выбираю нижнюю правую вершину (3).

Каждая вершина дерева хранится в отдельном файле. Стркутура каталогов повторяет путь до вершины, т.е. вышеуказанная
вершина физически хранится в "root/0/2/3.terrain".

Когда нужно сгенерировать новую вершину, но она не помещается в дерево, я его увеличиваю. Алгоритм роста дерева:

1. Создаю временню папку
2. Перемещаю в нее все содержимое от корня.
3. Перемещаю папку в корень называя ее необходимой ориентацией.

Использование файловой системы делает процесс увеличения дерева реактивным: данные не перемещаются, меняются только пути.


Пример
------
Есть узел по пути "1/3". Задача - создать узел справа от него. Мы не можем создать узел справа (поскольку "1/3" - это крайний правый узел).


          Этап                 |    Состояние файлов
    -----------------------------------------------------------
                               |
    Начальное состояние        |       root/1/3.terrain
                               |
    -----------------------------------------------------------
                               |
    Все во временную папку     |       tmp/1/3.terrain
                               |
    -----------------------------------------------------------
                               |
    Создаем новый узел         |      tmp/1/3.terrain
                               |      root/0   (папка)
                               |
    -----------------------------------------------------------
                               |
    Перемещаем все в новый     |      root/0/1/3.terrain
    узел                       |
                               |
    -----------------------------------------------------------
                               |
    Теперь можно создать узел  |      root/0/1/3.terrain
    справа                     |      root/1/0/2.terrain
                               |
    -----------------------------------------------------------


Помимо ".terrain" файлов (вершин), на диск сохраняются "info" файлы - в них содержатся суммарные веса
для каждого ребенка. В итоге, струткура файлов для примера выше выглядит так:


    root/info
    root/0/info
    root/0/1/info
    root/0/1/3.terrain
    root/1/info
    root/1/0/info
    root/1/0/2.terrain


Первичное создание блоков
=========================

Поскольку существует задача располагать объекты в зависимости от удаленности от других объектов,
я расчитываю и запомнаю вес объекта в блоке. Это позволяет избежать необходимости итерировать
по всем объектам при расчете позции новых объектов. Относительная ероятность появления нового объекта
равна objectWeight / totalWeight, где totalWeight - сумма всех весов в блоке.

Веса расчитываются по следующему алгоритму:

1. Заполняю матрицц terrain пустыми объектами. Вес пустого объекта = MAX_WEIGHT (выбирается произвольно).
2. Заволняю матрицу непустыми объектами. Вес такого объекта  = 0 (на его месте ничего не может появится).
3. Итерирую по матрица и для каждого x,y еще итерирую по ближайшим объектам:
    4. Уменьшаю вес объекта пропорционально удаленности от x,y

Помимо расчета весов в самом блоке, я учитваю веса объектов в соседних блоках, а при создании новых блоков - 
обновляю соседние.

При сохранении блока (в ".terrain" файл) я еще обновляю все его родительские узлы ("info" файлы) 
и рекрсивно добавляю вес блока до корневого узла. Это нужно для определения конкретного блока, в 
который нужно добавить объект.


Выбор блока для создания объекта
================================

Для выбора необходимого блока я рекурсивно спускаюсь по ветке вниз (только по одной ветке). Для этого на каждом 
шаге я выбираю ориентацию на основе весов (которые хранятся в "info" файлах). Далее я выбираю позицию на основе
весов в блоке.

Поскольку вероятность события равна произведению вероятностей всех его составляющих, этот метод дает тот же
результат, что и перебор всех возможный позиций в мире. Однако он значительно быстрее блягодаря использованию дерева.

Текущеие веса для объектов можно увидеть, если переключится в режим сцены во время запущенной игры (возможно 
нужно приблизить камеру что бы метки появились).


Мутабельность путей
===================
Из-за того, что дерево постоянно растет, в разное время жизни приложения, пути до одних и тех же вершин могут отличаться.
При этом я хочу иметь постоянную ссылку на узел независимо от времени жизни приложения ( например, добавления
нового объекта травы мне нужна связь узлов с элементами сцены).

Что бы это было возможно я храню основную ветку дерева - путь по которому дерево росло. Путь от узла до этой ветки
будет оставаться неизменным, и его можно использовать для постоянных связей.


Сеть
====

Код довольно легко допиливается до клиент-серверного.

1. Все перемещения происходят на сервере, клиент только передает input.
2. Если на клиенте нет информации о блоке, он спрашивает ее у сервера (а не читает с файловой системы).
3. Данные о текущей позиции и блоке ханятся на сервере, а не в PlayerPrefs.
4. При создании нового объекта сервер сообщает об этом клиенту, а не добавляет его на сцену.
5. Добавляется функционал для авторизации/идентификации клиентов.

