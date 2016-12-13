import cognitive_face as CF
import os.path

KEY = 'f34cccf1e2474f468de0b5cce660add5'  # Replace with a valid Subscription Key here.
CF.Key.set(KEY)

path = os.path.abspath('pictures/foo.jpg')

result = CF.face.detect(path)
print result
